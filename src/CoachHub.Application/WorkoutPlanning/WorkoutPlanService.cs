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
        await ValidateExercisesAsync(aggregate, cancellationToken);
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
        await ValidateExercisesAsync(aggregate, cancellationToken);
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
        await ValidateExercisesAsync(aggregate, cancellationToken);
        await repository.AddRevisionAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Duplicates the current version into a new plan, which starts at version 1.</summary>
    public async Task<WorkoutPlanResponse> CopyAsync(
        Guid id, CopyWorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var source = await RequiredAsync(id, null, false, cancellationToken);
        await ValidateClientAsync(source.Plan.CoachId, input.ClientId, cancellationToken);
        var copyInput = new WorkoutPlanInput(input.NameEn, input.NameAr, input.ClientId,
            source.Notes.Select(x => new WorkoutPlanNoteInput(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
            source.Days.OrderBy(x => x.Order).Select(day => new WorkoutDayInput(
                day.Id, day.NameEn, day.NameAr, day.Subtitle, day.Notes, day.Order,
                source.Exercises.Where(x => x.WorkoutDayId == day.Id).OrderBy(x => x.Order).Select(x =>
                    new WorkoutExerciseInput(x.Id, x.ExerciseId, x.Order, x.Sets,
                        x.Repetitions, x.Rest, x.Tempo, x.RpeRir, x.Notes)).ToArray())).ToArray(),
            null, source.Revision.StartDate, source.Revision.EndDate, source.Revision.Status);
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
            var exercises = input.Days.SelectMany(day => day.Exercises.Select(x => WorkoutExercise.Create(
                x.Id, revisionId, day.Id, x.ExerciseId, x.Order, x.Sets, x.Repetitions,
                x.Rest, x.Tempo, x.RpeRir, x.Notes))).ToArray();
            return new(plan, revision, revision.VersionNumber, notes, days, exercises);
        }
        catch (ArgumentException exception) { throw Validation("workoutPlan", exception.Message); }
    }

    /// <summary>The same content under fresh identifiers, for copies and new versions.</summary>
    private static WorkoutPlanInput WithNewIdentifiers(WorkoutPlanInput input) => input with
    {
        Notes = input.Notes.Select(x => x with { Id = Guid.NewGuid() }).ToArray(),
        Days = input.Days.Select(day => day with
        {
            Id = Guid.NewGuid(),
            Exercises = day.Exercises.Select(x => x with { Id = Guid.NewGuid() }).ToArray()
        }).ToArray()
    };

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
            .Concat(input.Days.SelectMany(x => x.Exercises).Select(x => x.Id)).ToArray();
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            errors["ids"] = ["Every nested identifier must be non-empty and unique."];
        CheckOrders(input.Notes.Select(x => x.Order), "notes.order", errors);
        CheckOrders(input.Days.Select(x => x.Order), "days.order", errors);
        foreach (var day in input.Days)
            CheckOrders(day.Exercises.Select(x => x.Order), $"days.{day.Id}.exercises.order", errors);
        return errors;
    }

    private static void CheckOrders(IEnumerable<int> orders, string key, IDictionary<string, string[]> errors)
    {
        var values = orders.ToArray();
        if (values.Any(x => x < 0) || values.Distinct().Count() != values.Length)
            errors[key] = ["Orders must be non-negative and unique within their parent."];
    }

    private async Task ValidateExercisesAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        var ids = aggregate.Exercises.Select(x => x.ExerciseId).Distinct().ToArray();
        var exercises = await repository.FindExercisesAsync(ids, cancellationToken);
        if (ids.Any(x => !exercises.ContainsKey(x))) throw Validation("exerciseIds", "One or more exercises do not exist.");
    }

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
        var catalog = await repository.FindExercisesAsync(
            aggregate.Exercises.Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);
        var authors = await repository.FindAuthorNamesAsync(Authors([aggregate.Revision]), cancellationToken);
        return new(aggregate.Plan.Id, aggregate.Plan.CoachId, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
            aggregate.Plan.ClientId, aggregate.Plan.CreatedAt,
            PlanRevisions.Info(aggregate.Revision, authors), aggregate.CurrentVersionNumber,
            aggregate.Notes.OrderBy(x => x.Order).Select(x => new WorkoutPlanNoteResponse(
                x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
            aggregate.Days.OrderBy(x => x.Order).Select(day => new WorkoutDayResponse(
                day.Id, day.NameEn, day.NameAr, day.Subtitle, day.Notes, day.Order,
                aggregate.Exercises.Where(x => x.WorkoutDayId == day.Id).OrderBy(x => x.Order).Select(item =>
                {
                    var exercise = catalog[item.ExerciseId];
                    return new WorkoutExerciseResponse(item.Id, item.ExerciseId, exercise.NameEn,
                        exercise.NameAr, exercise.ExerciseCategoryId, exercise.MediaId, exercise.YouTubeUrl,
                        exercise.IsActive, item.Order, item.Sets, item.Repetitions, item.Rest,
                        item.Tempo, item.RpeRir, item.Notes);
                }).ToArray())).ToArray());
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
