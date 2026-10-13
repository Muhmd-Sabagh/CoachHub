using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Common;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

/// <summary>
/// Workout plans with version history (D4, §59): "save in place" rewrites the current version;
/// "save as new version" archives it (read-only from then on) and adds the next.
/// </summary>
public sealed class WorkoutPlanService(
    IWorkoutPlanRepository repository, CoachOwnership ownership, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<WorkoutPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await MapAsync(await RequiredAsync(id, null, false, cancellationToken), cancellationToken);

    public async Task<WorkoutPlanResponse> GetVersionAsync(Guid id, int versionNumber, CancellationToken cancellationToken) =>
        await MapAsync(await RequiredAsync(id, versionNumber, false, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<PlanRevisionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var revisions = await repository.ListRevisionsAsync(id, cancellationToken) ?? throw new NotFoundException("Workout plan", id);
        var names = await repository.FindAuthorNamesAsync(Authors(revisions), cancellationToken);
        return revisions.OrderByDescending(x => x.VersionNumber).Select(x => PlanRevisions.Info(x, names)).ToArray();
    }

    public async Task<WorkoutPlanResponse> CreateAsync(WorkoutPlanInput input, CancellationToken cancellationToken) =>
        await CreateForCoachAsync(await ResolveCoachAsync(input.ClientId, input.CoachId, cancellationToken), input, cancellationToken);

    private async Task<WorkoutPlanResponse> CreateForCoachAsync(Guid coachId, WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        WorkoutPlan? plan = null;
        WorkoutPlanRevision? revision = null;
        TryDomain(() =>
        {
            plan = WorkoutPlan.Create(coachId, input.NameEn, input.NameAr, input.ClientId, now);
            revision = WorkoutPlanRevision.Create(plan.Id, 1, input.Status, input.StartDate, input.EndDate, currentUser.UserId, now);
        });
        var aggregate = Build(plan!, revision!, input);
        await ValidateExercisesAsync(aggregate, new HashSet<Guid>(), cancellationToken);
        await repository.AddAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Save in place: the current version takes the new content, dates and status.</summary>
    public async Task<WorkoutPlanResponse> SaveInPlaceAsync(
        Guid id, WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var existing = await RequiredAsync(id, null, true, cancellationToken);
        PlanRevisions.EnsureBase(existing.Revision, input.BaseVersionNumber);
        await ValidateClientAsync(existing.Plan.CoachId, input.ClientId, cancellationToken);
        TryDomain(() =>
        {
            existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId);
            existing.Revision.Update(input.Status, input.StartDate, input.EndDate, timeProvider.GetUtcNow());
        });
        var aggregate = Build(existing.Plan, existing.Revision, input);
        await ValidateExercisesAsync(aggregate, ExerciseIds(existing), cancellationToken);
        await repository.ReplaceContentAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Save as new version: the content becomes version N+1 and version N is archived unchanged.</summary>
    public async Task<WorkoutPlanResponse> SaveAsNewVersionAsync(
        Guid id, WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var existing = await RequiredAsync(id, null, true, cancellationToken);
        PlanRevisions.EnsureBase(existing.Revision, input.BaseVersionNumber);
        await ValidateClientAsync(existing.Plan.CoachId, input.ClientId, cancellationToken);
        ThrowIfInvalid(input);
        var now = timeProvider.GetUtcNow();
        WorkoutPlanRevision? next = null;
        TryDomain(() =>
        {
            existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId);
            next = WorkoutPlanRevision.Create(existing.Plan.Id, existing.Revision.VersionNumber + 1,
                input.Status, input.StartDate, input.EndDate, currentUser.UserId, now);
        });
        existing.Revision.Archive(now);
        var aggregate = Build(existing.Plan, next!, WithNewIdentifiers(input));
        await ValidateExercisesAsync(aggregate, ExerciseIds(existing), cancellationToken);
        await repository.AddRevisionAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Duplicates the current version into a new plan, which starts at version 1.</summary>
    public async Task<WorkoutPlanResponse> CopyAsync(
        Guid id, CopyWorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var source = await RequiredAsync(id, null, false, cancellationToken);
        await ValidateClientAsync(source.Plan.CoachId, input.ClientId, cancellationToken);
        var copyInput = ToInput(source) with { NameEn = input.NameEn, NameAr = input.NameAr, ClientId = input.ClientId };
        return await CreateForCoachAsync(source.Plan.CoachId, WithNewIdentifiers(copyInput), cancellationToken);
    }

    public async Task<WorkoutPlanResponse> AssignAsync(
        Guid id, Guid? clientId, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, null, true, cancellationToken);
        await ValidateClientAsync(aggregate.Plan.CoachId, clientId, cancellationToken);
        TryDomain(() => aggregate.Plan.Assign(clientId));
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Shows or hides a note of the current version in the PDF (a save in place).</summary>
    public async Task<WorkoutPlanResponse> SetNoteActiveAsync(
        Guid id, Guid noteId, bool isActive, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, null, true, cancellationToken);
        var note = aggregate.Notes.SingleOrDefault(x => x.Id == noteId)
            ?? throw new NotFoundException("Workout plan note", noteId);
        note.SetActive(isActive);
        aggregate.Revision.Touch(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await RequiredAsync(id, null, false, cancellationToken);
        await repository.DeleteAsync(id, cancellationToken);
    }

    private static WorkoutPlanAggregate Build(WorkoutPlan plan, WorkoutPlanRevision revision, WorkoutPlanInput input)
    {
        ThrowIfInvalid(input);
        var revisionId = revision.Id;
        try
        {
            var notes = input.Notes.Select(x => WorkoutPlanNote.Create(x.Id, revisionId, x.Text, x.Order, x.IsActive)).ToArray();
            var days = input.Days.Select(x => WorkoutDay.Create(
                x.Id, revisionId, x.NameEn, x.NameAr, x.Subtitle, x.Notes, x.Order)).ToArray();
            var groups = input.Days.SelectMany(day => (day.SetGroups ?? []).Select(x => WorkoutSetGroup.Create(
                x.Id, revisionId, day.Id, x.Type, x.RestSeconds, x.Notes))).ToArray();
            var exercises = input.Days.SelectMany(day => day.Exercises.Select(x => WorkoutExercise.Create(
                x.Id, revisionId, day.Id, x.ExerciseId, x.Order, x.Notes, x.Block, x.SetGroupId))).ToArray();
            var sets = input.Days.SelectMany(day => day.Exercises.SelectMany(exercise => exercise.Sets.Select(x =>
                PrescribedSet.Create(x.Id, revisionId, exercise.Id, x.Order, new SetPrescription(
                    x.Type, x.RepsMin, x.RepsMax, x.LoadKg, x.Rir, x.Rpe, x.Tempo, x.RestSeconds, x.DurationSeconds,
                    x.IsSkippable, x.Notes))))).ToArray();
            return new(plan, revision, revision.VersionNumber, notes, days, exercises, groups, sets);
        }
        catch (ArgumentException exception) { throw Validation(exception.ParamName ?? "workoutPlan", exception.Message); }
    }

    /// <summary>The version's content as editor input, keeping its identifiers.</summary>
    private static WorkoutPlanInput ToInput(WorkoutPlanAggregate source) => new(
        source.Plan.NameEn, source.Plan.NameAr, source.Plan.ClientId,
        source.Notes.Select(x => new WorkoutPlanNoteInput(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
        source.Days.OrderBy(x => x.Order).Select(day => new WorkoutDayInput(
            day.Id, day.NameEn, day.NameAr, day.Subtitle, day.Notes, day.Order,
            source.Exercises.Where(x => x.WorkoutDayId == day.Id).OrderBy(x => x.Order).Select(exercise =>
                new WorkoutExerciseInput(exercise.Id, exercise.ExerciseId, exercise.Order, exercise.Notes,
                    source.Sets.Where(x => x.WorkoutExerciseId == exercise.Id).OrderBy(x => x.Order).Select(set =>
                        new PrescribedSetInput(set.Id, set.Order, set.Type, set.RepsMin, set.RepsMax, set.LoadKg, set.Rir,
                            set.Rpe, set.Tempo, set.RestSeconds, set.DurationSeconds, set.IsSkippable, set.Notes)).ToArray(),
                    exercise.Block, exercise.SetGroupId)).ToArray(),
            source.SetGroups.Where(x => x.WorkoutDayId == day.Id).Select(group =>
                new WorkoutSetGroupInput(group.Id, group.Type, group.RestSeconds, group.Notes)).ToArray())).ToArray(),
        null, source.Revision.StartDate, source.Revision.EndDate,
        source.Revision.IsArchived ? PlanRevisionStatus.Active : source.Revision.Status);

    /// <summary>The same content under fresh identifiers, for copies and new versions.</summary>
    private static WorkoutPlanInput WithNewIdentifiers(WorkoutPlanInput input)
    {
        var groups = input.Days.SelectMany(x => x.SetGroups ?? []).ToDictionary(x => x.Id, _ => Guid.NewGuid());
        return input with
        {
            Notes = input.Notes.Select(x => x with { Id = Guid.NewGuid() }).ToArray(),
            Days = input.Days.Select(day => day with
            {
                Id = Guid.NewGuid(),
                SetGroups = day.SetGroups?.Select(x => x with { Id = groups[x.Id] }).ToArray(),
                Exercises = day.Exercises.Select(x => x with
                {
                    Id = Guid.NewGuid(),
                    SetGroupId = x.SetGroupId is { } group && groups.TryGetValue(group, out var newGroup) ? newGroup : x.SetGroupId,
                    Sets = x.Sets.Select(set => set with { Id = Guid.NewGuid() }).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }

    private static void ThrowIfInvalid(WorkoutPlanInput input)
    {
        var errors = ValidateShape(input);
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static Dictionary<string, string[]> ValidateShape(WorkoutPlanInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Days.Count == 0) errors["days"] = ["At least one workout day is required."];
        var ids = input.Notes.Select(x => x.Id).Concat(input.Days.Select(x => x.Id))
            .Concat(input.Days.SelectMany(x => x.Exercises).Select(x => x.Id))
            .Concat(input.Days.SelectMany(x => x.SetGroups ?? []).Select(x => x.Id))
            .Concat(input.Days.SelectMany(x => x.Exercises).SelectMany(x => x.Sets).Select(x => x.Id)).ToArray();
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            errors["ids"] = ["Every nested identifier must be non-empty and unique."];
        CheckOrders(input.Notes.Select(x => x.Order), "notes.order", errors);
        CheckOrders(input.Days.Select(x => x.Order), "days.order", errors);
        foreach (var day in input.Days)
        {
            CheckOrders(day.Exercises.Select(x => x.Order), $"days.{day.Id}.exercises.order", errors);
            foreach (var exercise in day.Exercises)
            {
                if (exercise.Sets.Count is 0 or > WorkoutExercise.MaximumSets)
                    errors[$"exercises.{exercise.Id}.sets"] = [$"An exercise has 1–{WorkoutExercise.MaximumSets} sets."];
                CheckOrders(exercise.Sets.Select(x => x.Order), $"exercises.{exercise.Id}.sets.order", errors);
            }
            CheckStructure(day, errors);
        }
        return errors;
    }

    /// <summary>
    /// §16: warm-up exercises come first and cool-down last; a set group's exercises are consecutive, in one
    /// block, and as many as its type takes (superset 2, giant set 3+).
    /// </summary>
    private static void CheckStructure(WorkoutDayInput day, IDictionary<string, string[]> errors)
    {
        var ordered = day.Exercises.OrderBy(x => x.Order).ToArray();
        if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.Block > pair.Second.Block))
            errors[$"days.{day.Id}.blocks"] = ["Warm-up exercises come first and cool-down exercises last."];
        var groups = (day.SetGroups ?? []).ToDictionary(x => x.Id);
        if (ordered.Any(x => x.SetGroupId is { } group && !groups.ContainsKey(group)))
        {
            errors[$"days.{day.Id}.setGroups"] = ["A set group must belong to the exercise's day."];
            return;
        }
        foreach (var group in groups.Values)
        {
            var positions = ordered.Select((exercise, index) => (exercise, index))
                .Where(x => x.exercise.SetGroupId == group.Id).ToArray();
            var key = $"setGroups.{group.Id}";
            if (!Enum.IsDefined(group.Type)) errors[key] = ["Unknown set group type."];
            else if (!WorkoutSetGroup.Fits(group.Type, positions.Length))
                errors[key] = [group.Type == SetGroupType.Superset
                    ? "A superset has exactly 2 exercises."
                    : $"A giant set has 3 to {WorkoutSetGroup.MaximumExercises} exercises."];
            else if (positions.Zip(positions.Skip(1)).Any(pair => pair.Second.index != pair.First.index + 1))
                errors[key] = ["A set group's exercises follow each other."];
            else if (positions.Select(x => x.exercise.Block).Distinct().Count() > 1)
                errors[key] = ["A set group's exercises are in one block."];
        }
    }

    private static void CheckOrders(IEnumerable<int> orders, string key, IDictionary<string, string[]> errors)
    {
        var values = orders.ToArray();
        if (values.Any(x => x < 0) || values.Distinct().Count() != values.Length)
            errors[key] = ["Orders must be non-negative and unique within their parent."];
    }

    /// <summary>
    /// Every exercise must be one the caller can see (platform or the coach's own), except exercises the version
    /// already used: a transferred client's plan may keep the previous coach's custom exercises (D7).
    /// </summary>
    private async Task ValidateExercisesAsync(
        WorkoutPlanAggregate aggregate, IReadOnlySet<Guid> alreadyUsed, CancellationToken cancellationToken)
    {
        var ids = ExerciseIds(aggregate).ToArray();
        var visible = await repository.FindExercisesAsync(ids, cancellationToken);
        var missing = ids.Where(id => !visible.ContainsKey(id)).ToArray();
        if (missing.Length == 0) return;
        if (missing.Any(id => !alreadyUsed.Contains(id)) ||
            (await repository.FindReferencedExercisesAsync(missing, cancellationToken)).Count != missing.Length)
            throw Validation("exerciseIds", "One or more exercises do not exist.");
    }

    private static HashSet<Guid> ExerciseIds(WorkoutPlanAggregate aggregate) =>
        aggregate.Exercises.Select(x => x.ExerciseId).ToHashSet();

    /// <summary>A client-assigned plan belongs to the client's coach; a template to the creating coach.</summary>
    private async Task<Guid> ResolveCoachAsync(Guid? clientId, Guid? requestedCoachId, CancellationToken cancellationToken)
    {
        if (clientId is { } id)
        {
            var clientCoachId = await repository.FindClientCoachIdAsync(id, cancellationToken)
                ?? throw Validation("clientId", "The selected client does not exist.");
            if (requestedCoachId.HasValue && requestedCoachId.Value != clientCoachId)
                throw Validation("coachId", "The client belongs to another coach.");
            return clientCoachId;
        }
        return await ownership.ForNewRecordAsync(requestedCoachId, cancellationToken);
    }

    private async Task ValidateClientAsync(Guid planCoachId, Guid? clientId, CancellationToken cancellationToken)
    {
        if (clientId is not { } id) return;
        var clientCoachId = await repository.FindClientCoachIdAsync(id, cancellationToken)
            ?? throw Validation("clientId", "The selected client does not exist.");
        if (clientCoachId != planCoachId)
            throw Validation("clientId", "The client belongs to another coach.");
    }

    private async Task<WorkoutPlanResponse> MapAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        // The plan is visible, so the exercises it references are readable as part of it.
        var catalog = await repository.FindReferencedExercisesAsync(ExerciseIds(aggregate), cancellationToken);
        var authors = await repository.FindAuthorNamesAsync(Authors([aggregate.Revision]), cancellationToken);
        return new(aggregate.Plan.Id, aggregate.Plan.CoachId, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
            aggregate.Plan.ClientId, aggregate.Plan.CreatedAt,
            PlanRevisions.Info(aggregate.Revision, authors), aggregate.CurrentVersionNumber,
            aggregate.Notes.OrderBy(x => x.Order).Select(x => new WorkoutPlanNoteResponse(
                x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
            aggregate.Days.OrderBy(x => x.Order).Select(day => MapDay(aggregate, day, catalog)).ToArray());
    }

    /// <summary>A day with its exercises in order; set groups are lettered A, B, … by first appearance.</summary>
    private static WorkoutDayResponse MapDay(
        WorkoutPlanAggregate aggregate, WorkoutDay day, IReadOnlyDictionary<Guid, Domain.Training.Exercise> catalog)
    {
        var exercises = aggregate.Exercises.Where(x => x.WorkoutDayId == day.Id).OrderBy(x => x.Order).ToArray();
        var letters = exercises.Where(x => x.SetGroupId.HasValue).Select(x => x.SetGroupId!.Value).Distinct()
            .Select((id, index) => (id, letter: ((char)('A' + index % 26)).ToString())).ToDictionary(x => x.id, x => x.letter);
        var positions = new Dictionary<Guid, int>();
        return new WorkoutDayResponse(day.Id, day.NameEn, day.NameAr, day.Subtitle, day.Notes, day.Order,
            exercises.Select(item =>
            {
                var exercise = catalog[item.ExerciseId];
                string? label = null;
                if (item.SetGroupId is { } group && letters.TryGetValue(group, out var letter))
                {
                    positions[group] = positions.GetValueOrDefault(group) + 1;
                    label = letter + positions[group];
                }
                return new WorkoutExerciseResponse(item.Id, item.ExerciseId, exercise.NameEn,
                    exercise.NameAr, exercise.ExerciseCategoryId, exercise.MediaId, exercise.YouTubeUrl,
                    exercise.IsActive, item.Order, item.Notes, item.Block, item.SetGroupId, label,
                    exercise.PrimaryMuscleGroupId,
                    aggregate.Sets.Where(x => x.WorkoutExerciseId == item.Id).OrderBy(x => x.Order).Select(set =>
                        new PrescribedSetResponse(set.Id, set.Order, set.Type, set.RepsMin, set.RepsMax, set.LoadKg,
                            set.Rir, set.Rpe, set.Tempo, set.RestSeconds, set.DurationSeconds, set.IsSkippable, set.Notes)).ToArray());
            }).ToArray(),
            aggregate.SetGroups.Where(x => x.WorkoutDayId == day.Id && letters.ContainsKey(x.Id)).OrderBy(x => letters[x.Id])
                .Select(group => new WorkoutSetGroupResponse(group.Id, group.Type, letters[group.Id], group.RestSeconds, group.Notes))
                .ToArray());
    }

    private static Guid[] Authors(IEnumerable<PlanRevision> revisions) =>
        revisions.Where(x => x.CreatedByUserId.HasValue).Select(x => x.CreatedByUserId!.Value).Distinct().ToArray();

    private async Task<WorkoutPlanAggregate> RequiredAsync(
        Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, versionNumber, tracking, cancellationToken)
            ?? (versionNumber is { } number
                ? throw new NotFoundException("Workout plan version", $"{id} v{number}")
                : throw new NotFoundException("Workout plan", id));
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
    private static void TryDomain(Action action)
    {
        try { action(); } catch (ArgumentException exception) { throw Validation("workoutPlan", exception.Message); }
    }
}
