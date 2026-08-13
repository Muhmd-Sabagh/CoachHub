using CoachHub.Application.Media;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Media;

public sealed class MediaRepository(CoachHubDbContext dbContext) : IMediaRepository
{
    public async Task AddAsync(MediaAsset media, CancellationToken cancellationToken)
    {
        dbContext.Set<MediaAsset>().Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<MediaAsset>().SingleOrDefaultAsync(
            media => media.Id == id,
            cancellationToken);
    }

    public async Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken)
    {
        dbContext.Set<MediaAsset>().Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
