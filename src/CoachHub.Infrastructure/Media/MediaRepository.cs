using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Media;
using CoachHub.Domain.Settings;
using CoachHub.Infrastructure.Auth.Persistence;
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
        // Brands and profile pictures are checked across tenants: the reference may be anyone's.
        if (await dbContext.Set<Brand>().IgnoreQueryFilters().AnyAsync(brand =>
                brand.LogoMediaId == media.Id || brand.FaviconMediaId == media.Id || brand.DefaultAvatarMediaId == media.Id,
                cancellationToken) ||
            await dbContext.Set<User>().AnyAsync(user => user.AvatarMediaId == media.Id, cancellationToken))
        {
            throw new ConflictException("Media used as a brand image or profile picture cannot be deleted here.");
        }
        dbContext.Set<MediaAsset>().Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
