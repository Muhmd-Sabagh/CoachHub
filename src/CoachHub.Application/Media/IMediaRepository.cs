using CoachHub.Domain.Media;

namespace CoachHub.Application.Media;

public interface IMediaRepository
{
    Task AddAsync(MediaAsset media, CancellationToken cancellationToken);

    Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken);
}
