using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

public sealed class WorkoutPlanService(IWorkoutPlanRepository repository, TimeProvider timeProvider)
{
    public async Task<WorkoutPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await MapAsync(await RequiredAsync(id, false, cancellationToken), cancellationToken);

    public async Task<WorkoutPlanResponse> CreateAsync(WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(input.ClientId, cancellationToken);
        WorkoutPlan? plan = null;
        TryDomain(() => plan = WorkoutPlan.Create(input.NameEn, input.NameAr, input.ClientId, timeProvider.GetUtcNow()));
        var aggregate = Build(plan!, input);
        await ValidateExercisesAsync(aggregate, cancellationToken);
        await repository.AddAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<WorkoutPlanResponse> UpdateAsync(
        Guid id, WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(input.ClientId, cancellationToken);
        var existing = await RequiredAsync(id, true, cancellationToken);
        TryDomain(() => existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId));
        var aggregate = Build(existing.Plan, input);
        await ValidateExercisesAsync(aggregate, cancellationToken);
        await repository.ReplaceChildrenAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<WorkoutPlanResponse> CopyAsync(
        Guid id, CopyWorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var source = await RequiredAsync(id, false, cancellationToken);
        var copyInput = new WorkoutPlanInput(input.NameEn, input.NameAr, input.ClientId,
            source.Notes.Select(x => new WorkoutPlanNoteInput(Guid.NewGuid(), x.Text, x.Order, x.IsActive)).ToArray(),
            source.Days.OrderBy(x => x.Order).Select(day => new WorkoutDayInput(
                Guid.NewGuid(), day.NameEn, day.NameAr, day.Subtitle, day.Notes, day.Order,
                source.Exercises.Where(x => x.WorkoutDayId == day.Id).OrderBy(x => x.Order).Select(x =>
                    new WorkoutExerciseInput(Guid.NewGuid(), x.ExerciseId, x.Order, x.Sets,
                        x.Repetitions, x.Rest, x.Tempo, x.RpeRir, x.Notes)).ToArray())).ToArray());
        return await CreateAsync(copyInput, cancellationToken);
    }

    public async Task<WorkoutPlanResponse> AssignAsync(
        Guid id, Guid? clientId, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(clientId, cancellationToken);
        var aggregate = await RequiredAsync(id, true, cancellationToken);
        TryDomain(() => aggregate.Plan.Assign(clientId));
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<WorkoutPlanResponse> SetNoteActiveAsync(
        Guid id, Guid noteId, bool isActive, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, true, cancellationToken);
        var note = aggregate.Notes.SingleOrDefault(x => x.Id == noteId)
            ?? throw new NotFoundException("Workout plan note", noteId);
        note.SetActive(isActive);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(await RequiredAsync(id, true, cancellationToken), cancellationToken);

    private static WorkoutPlanAggregate Build(WorkoutPlan plan, WorkoutPlanInput input)
    {
        var errors = ValidateShape(input);
        if (errors.Count > 0) throw new ValidationException(errors);
        try
        {
            var notes = input.Notes.Select(x => WorkoutPlanNote.Create(x.Id, plan.Id, x.Text, x.Order, x.IsActive)).ToArray();
            var days = input.Days.Select(x => WorkoutDay.Create(
                x.Id, plan.Id, x.NameEn, x.NameAr, x.Subtitle, x.Notes, x.Order)).ToArray();
            var exercises = input.Days.SelectMany(day => day.Exercises.Select(x => WorkoutExercise.Create(
                x.Id, day.Id, x.ExerciseId, x.Order, x.Sets, x.Repetitions,
                x.Rest, x.Tempo, x.RpeRir, x.Notes))).ToArray();
            return new(plan, notes, days, exercises);
        }
        catch (ArgumentException exception) { throw Validation("workoutPlan", exception.Message); }
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

    private async Task ValidateClientAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (clientId.HasValue && !await repository.ClientExistsAsync(clientId.Value, cancellationToken))
            throw Validation("clientId", "The selected client does not exist.");
    }

    private async Task<WorkoutPlanResponse> MapAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        var catalog = await repository.FindExercisesAsync(
            aggregate.Exercises.Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);
        return new(aggregate.Plan.Id, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
            aggregate.Plan.ClientId, aggregate.Plan.CreatedAt,
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

    private async Task<WorkoutPlanAggregate> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, tracking, cancellationToken) ?? throw new NotFoundException("Workout plan", id);
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
    private static void TryDomain(Action action)
    {
        try { action(); } catch (ArgumentException exception) { throw Validation("workoutPlan", exception.Message); }
    }
}
