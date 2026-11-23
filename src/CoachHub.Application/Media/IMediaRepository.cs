using CoachHub.Domain.Media;

namespace CoachHub.Application.Media;

public interface IMediaRepository
{
    Task AddAsync(MediaAsset media, CancellationToken cancellationToken);

    Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The client-area feature a client's file belongs to (P22): a progress photo, a chat attachment, a shared file or
    /// a form answer. Null for anything else.
    /// </summary>
    Task<CoachHub.Domain.Settings.ClientFeature?> OwningFeatureAsync(Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken);
}
