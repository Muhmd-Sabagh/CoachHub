using CoachHub.Application.Common.Models;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.WorkoutTracking;

public interface IWorkoutTrackingRepository
{
    /// <summary>The client's current coach, or null when the client is not visible to the caller.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Every version of the workout plans assigned to the client, with its number of training days.</summary>
    Task<IReadOnlyList<WorkoutVersion>> ListWorkoutVersionsAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>A visible workout with its exercises and sets.</summary>
    Task<WorkoutSession?> FindSessionAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<WorkoutSession?> FindInProgressAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>The client's workouts dated <paramref name="from"/>–<paramref name="to"/>, with exercises and sets.</summary>
    Task<IReadOnlyList<WorkoutSession>> ListSessionsAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<PagedResult<WorkoutSession>> PageSessionsAsync(Guid clientId, WorkoutSessionQuery query, CancellationToken cancellationToken);

    /// <summary>The client's completed workouts that include any of the exercises, with all their exercises and sets.</summary>
    Task<IReadOnlyList<WorkoutSession>> ListCompletedSessionsWithAsync(
        Guid clientId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken);

    /// <summary>The client's PRs, of the given exercises only when <paramref name="exerciseIds"/> is set.</summary>
    Task<IReadOnlyList<PersonalRecord>> ListRecordsAsync(
        Guid clientId, IReadOnlyCollection<Guid>? exerciseIds, bool tracking, CancellationToken cancellationToken);

    /// <summary>
    /// Exercises referenced by workouts the caller can already see, whoever owns them (after a transfer, a log can
    /// reference the previous coach's custom exercises).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Exercise>> FindReferencedExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>An exercise the caller can see in the catalog (platform exercises and the coach's own).</summary>
    Task<Exercise?> FindVisibleExerciseAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, MuscleName>> ListMuscleNamesAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new workout; a second workout in progress for the client is a conflict.</summary>
    Task AddAsync(WorkoutSession session, CancellationToken cancellationToken);
    void Remove(WorkoutSession session);
    void Add(IEnumerable<PersonalRecord> records);
    void Remove(IEnumerable<PersonalRecord> records);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
