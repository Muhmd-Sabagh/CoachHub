using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

public sealed record WorkoutPlanAggregate(
    WorkoutPlan Plan, IReadOnlyList<WorkoutPlanNote> Notes,
    IReadOnlyList<WorkoutDay> Days, IReadOnlyList<WorkoutExercise> Exercises);

public interface IWorkoutPlanRepository
{
    Task<WorkoutPlanAggregate?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, Exercise>> FindExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>The client's current coach, or null when the client is not visible.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    Task AddAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken);
    Task ReplaceChildrenAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken);
    Task DeleteAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
