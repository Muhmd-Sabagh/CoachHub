using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

/// <summary>A workout plan with the content of one of its versions.</summary>
public sealed record WorkoutPlanAggregate(
    WorkoutPlan Plan, WorkoutPlanRevision Revision, int CurrentVersionNumber,
    IReadOnlyList<WorkoutPlanNote> Notes, IReadOnlyList<WorkoutDay> Days, IReadOnlyList<WorkoutExercise> Exercises,
    IReadOnlyList<WorkoutSetGroup> SetGroups, IReadOnlyList<PrescribedSet> Sets);

public interface IWorkoutPlanRepository
{
    /// <summary>The plan with one version: the current one when <paramref name="versionNumber"/> is null.</summary>
    Task<WorkoutPlanAggregate?> FindAsync(Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken);
    /// <summary>Every version of a visible plan; null when the plan is not visible.</summary>
    Task<IReadOnlyList<WorkoutPlanRevision>?> ListRevisionsAsync(Guid planId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> FindAuthorNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    /// <summary>Exercises the caller can see (platform exercises and the coach's own).</summary>
    Task<IReadOnlyDictionary<Guid, Exercise>> FindExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>
    /// Exercises referenced by a plan the caller can already see, whoever owns them (after a client transfer a
    /// plan can reference the previous coach's custom exercises).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Exercise>> FindReferencedExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>The client's current coach, or null when the client is not visible.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    Task AddAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken);
    /// <summary>Save in place: replaces the content of the (tracked, current) version.</summary>
    Task ReplaceContentAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken);
    /// <summary>Save as new version: stores the archived previous version and the next one together.</summary>
    Task AddRevisionAsync(WorkoutPlanAggregate next, CancellationToken cancellationToken);
    /// <summary>Deletes the plan with its whole history.</summary>
    Task DeleteAsync(Guid planId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
