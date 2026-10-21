using CoachHub.Domain.Common;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Progress;

public interface IProgressRepository
{
    /// <summary>The client's current coach, or null when the client is not visible to the caller.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>The client's entries of one kind, all of them or those dated <paramref name="from"/>–<paramref name="to"/>.</summary>
    Task<IReadOnlyList<T>> ListAsync<T>(Guid clientId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
        where T : ProgressEntry;

    /// <summary>The client's entries of one kind on a day (tracked, for changes).</summary>
    Task<IReadOnlyList<T>> FindOnAsync<T>(Guid clientId, DateOnly date, CancellationToken cancellationToken) where T : ProgressEntry;

    Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken) where T : Entity;

    Task<IReadOnlyList<ProgressPhoto>> ListPhotosAsync(Guid clientId, PhotoPose? pose, CancellationToken cancellationToken);
    Task<LifestyleTargets?> FindTargetsAsync(Guid clientId, CancellationToken cancellationToken);

    void Add(Entity entity);
    void Remove(Entity entity);

    /// <summary>Saves; two writes of the same day at once are a conflict.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
