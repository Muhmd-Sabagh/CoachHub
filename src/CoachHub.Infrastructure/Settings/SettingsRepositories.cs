using CoachHub.Application.Settings;
using CoachHub.Domain.Media;
using CoachHub.Domain.Settings;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Settings;

/// <summary>
/// Brands are read across tenants on purpose: the platform brand is everyone's default, a client
/// sees their coach's brand, and the login page shows a brand before anyone signs in. Writes
/// still pass the tenant save guard.
/// </summary>
public sealed class BrandRepository(CoachHubDbContext db) : IBrandRepository
{
    private IQueryable<Brand> Brands => db.Set<Brand>().IgnoreQueryFilters();

    public Task<Brand?> FindPlatformAsync(CancellationToken cancellationToken) =>
        Brands.SingleOrDefaultAsync(brand => brand.CoachId == null, cancellationToken);

    public Task<Brand?> FindForCoachAsync(Guid coachId, CancellationToken cancellationToken) =>
        Brands.SingleOrDefaultAsync(brand => brand.CoachId == coachId, cancellationToken);

    public async Task<Guid?> FindCoachByHostAsync(string? subdomain, string? customDomain, CancellationToken cancellationToken) =>
        await Brands.AsNoTracking()
            .Where(brand => brand.CoachId != null &&
                ((subdomain != null && brand.Subdomain == subdomain) ||
                 (customDomain != null && brand.CustomDomain == customDomain)))
            .Select(brand => brand.CoachId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HostTakenAsync(string? subdomain, string? customDomain, Guid exceptBrandId, CancellationToken cancellationToken) =>
        Brands.AnyAsync(brand => brand.Id != exceptBrandId &&
            ((subdomain != null && brand.Subdomain == subdomain) ||
             (customDomain != null && brand.CustomDomain == customDomain)),
            cancellationToken);

    public async Task<BrandImageFile?> FindBrandImageAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var used = await Brands.AnyAsync(brand =>
            brand.LogoMediaId == mediaId || brand.FaviconMediaId == mediaId || brand.DefaultAvatarMediaId == mediaId,
            cancellationToken);
        if (!used) return null;
        return await db.Set<MediaAsset>().IgnoreQueryFilters().AsNoTracking()
            .Where(media => media.Id == mediaId)
            .Select(media => new BrandImageFile(media.StorageKey, media.ContentType, media.OriginalFileName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Brand brand, CancellationToken cancellationToken)
    {
        db.Set<Brand>().Add(brand);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public sealed class SettingsRepository(CoachHubDbContext db) : ISettingsRepository
{
    public Task<PlatformSettings?> FindPlatformAsync(CancellationToken cancellationToken) =>
        db.Set<PlatformSettings>().SingleOrDefaultAsync(settings => settings.Id == PlatformSettings.SingletonId, cancellationToken);

    /// <summary>Tenant-filtered: a coach reaches only their own settings.</summary>
    public Task<CoachSettings?> FindCoachAsync(Guid coachId, CancellationToken cancellationToken) =>
        db.Set<CoachSettings>().SingleOrDefaultAsync(settings => settings.CoachId == coachId, cancellationToken);

    public void Add(object settings) => db.Add(settings);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
