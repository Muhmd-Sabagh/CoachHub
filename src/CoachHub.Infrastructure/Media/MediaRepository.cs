using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Assessments;
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
        if (await dbContext.Set<FormAnswer>()
                .AnyAsync(answer => answer.MediaId == media.Id, cancellationToken))
        {
            throw new ConflictException(
                "Media referenced by a submitted assessment cannot be deleted.");
        }
        dbContext.Set<MediaAsset>().Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
