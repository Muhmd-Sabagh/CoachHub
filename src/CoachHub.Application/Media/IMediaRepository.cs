using CoachHub.Domain.Media;

namespace CoachHub.Application.Media;

public interface IMediaRepository
{
    Task AddAsync(MediaAsset media, CancellationToken cancellationToken);

    Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every client-area feature that holds the file (P22): a progress photo, a chat attachment, a shared file or a form
    /// answer. One file may be linked from several; empty for anything else.
    /// </summary>
    Task<IReadOnlyList<CoachHub.Domain.Settings.ClientFeature>> OwningFeaturesAsync(Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken);
}
