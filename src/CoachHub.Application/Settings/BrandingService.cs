using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Settings;

/// <summary>
/// White-label branding (D1, §63): the platform brand is the default, a coach's brand overrides
/// it for the coach and their clients, and hosts can select a coach's brand before sign-in.
/// </summary>
public sealed class BrandingService(
    IBrandRepository brands,
    ITenantResolver tenants,
    ICoachRepository coaches,
    CoachOwnership ownership,
    ICurrentUser currentUser,
    MediaService media,
    IMediaStorage storage,
    TimeProvider timeProvider)
{
    public const long MaximumImageBytes = 1024 * 1024;
    private static readonly HashSet<string> ImageTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    /// <summary>The brand for a request: the caller's coach, else the host's coach, else the platform.</summary>
    public async Task<EffectiveBrand> ForRequestAsync(string? host, CancellationToken cancellationToken) =>
        await EffectiveAsync(await tenants.ResolveCoachAsync(host, cancellationToken), cancellationToken);

    /// <summary>A coach's brand over the platform brand over built-in defaults; null coach is the platform.</summary>
    public async Task<EffectiveBrand> EffectiveAsync(Guid? coachId, CancellationToken cancellationToken)
    {
        var platform = await brands.FindPlatformAsync(cancellationToken);
        var coach = coachId is { } id ? await brands.FindForCoachAsync(id, cancellationToken) : null;
        string? Pick(Func<Brand, string?> field) => (coach is null ? null : field(coach)) ?? (platform is null ? null : field(platform));
        Guid? PickImage(BrandImage image) => coach?.ImageOf(image) ?? platform?.ImageOf(image);

        return new EffectiveBrand(
            coach is null ? null : coachId,
            coach?.BrandName ?? platform?.BrandName ?? BrandDefaults.BrandName,
            Pick(brand => brand.PrimaryColor) ?? BrandDefaults.PrimaryColor,
            Pick(brand => brand.SecondaryColor) ?? BrandDefaults.SecondaryColor,
            PickImage(BrandImage.Logo),
            PickImage(BrandImage.Favicon),
            PickImage(BrandImage.DefaultAvatar),
            Pick(brand => brand.LoginHeadline),
            Pick(brand => brand.LoginMessage),
            Pick(brand => brand.DashboardWelcome),
            Pick(brand => brand.EmailHeader),
            Pick(brand => brand.EmailFooter));
    }

    public async Task<BrandResponse> GetPlatformAsync(CancellationToken cancellationToken) =>
        Map(await brands.FindPlatformAsync(cancellationToken), null, BrandDefaults.BrandName);

    /// <summary>A coach edits their own brand; an Administrator names the coach.</summary>
    public async Task<BrandResponse> GetCoachAsync(Guid? coachId, CancellationToken cancellationToken)
    {
        var owner = await CoachAsync(coachId, cancellationToken);
        return Map(await brands.FindForCoachAsync(owner, cancellationToken), owner, await CoachNameAsync(owner, cancellationToken));
    }

    public async Task<BrandResponse> UpdatePlatformAsync(BrandInput input, CancellationToken cancellationToken)
    {
        var brand = await brands.FindPlatformAsync(cancellationToken);
        return await SaveAsync(brand, null, input, cancellationToken);
    }

    public async Task<BrandResponse> UpdateCoachAsync(Guid? coachId, BrandInput input, CancellationToken cancellationToken)
    {
        var owner = await CoachAsync(coachId, cancellationToken);
        return await SaveAsync(await brands.FindForCoachAsync(owner, cancellationToken), owner, input, cancellationToken);
    }

    /// <summary>Administrator only (the API enforces it): hosts are platform infrastructure.</summary>
    public async Task<BrandResponse> SetHostsAsync(Guid coachId, BrandHostsInput input, CancellationToken cancellationToken)
    {
        var owner = await CoachAsync(coachId, cancellationToken);
        var brand = await brands.FindForCoachAsync(owner, cancellationToken) ?? await CreateAsync(owner, cancellationToken);
        DomainValidation.Run(() => brand.SetHosts(input.Subdomain, input.CustomDomain, Now), "hosts");
        if (await brands.HostTakenAsync(brand.Subdomain, brand.CustomDomain, brand.Id, cancellationToken))
            throw new ConflictException("Another coach already uses this subdomain or domain.");
        await brands.SaveChangesAsync(cancellationToken);
        return Map(brand, owner, brand.BrandName);
    }

    public async Task<BrandResponse> SetPlatformImageAsync(BrandImage image, BrandImageUpload upload, CancellationToken cancellationToken)
    {
        var brand = await brands.FindPlatformAsync(cancellationToken) ?? await CreateAsync(null, cancellationToken);
        await ReplaceImageAsync(brand, image, upload, MediaOwner.Platform(currentUser.UserId), cancellationToken);
        return Map(brand, null, brand.BrandName);
    }

    public async Task<BrandResponse> SetCoachImageAsync(Guid? coachId, BrandImage image, BrandImageUpload upload, CancellationToken cancellationToken)
    {
        var owner = await CoachAsync(coachId, cancellationToken);
        var brand = await brands.FindForCoachAsync(owner, cancellationToken) ?? await CreateAsync(owner, cancellationToken);
        await ReplaceImageAsync(brand, image, upload, MediaOwner.CoachLibrary(owner, currentUser.UserId), cancellationToken);
        return Map(brand, owner, brand.BrandName);
    }

    public async Task<BrandResponse> RemovePlatformImageAsync(BrandImage image, CancellationToken cancellationToken)
    {
        var brand = await brands.FindPlatformAsync(cancellationToken);
        if (brand is not null) await RemoveImageAsync(brand, image, cancellationToken);
        return Map(brand, null, BrandDefaults.BrandName);
    }

    public async Task<BrandResponse> RemoveCoachImageAsync(Guid? coachId, BrandImage image, CancellationToken cancellationToken)
    {
        var owner = await CoachAsync(coachId, cancellationToken);
        var brand = await brands.FindForCoachAsync(owner, cancellationToken);
        if (brand is not null) await RemoveImageAsync(brand, image, cancellationToken);
        return Map(brand, owner, await CoachNameAsync(owner, cancellationToken));
    }

    /// <summary>
    /// Opens a brand image for anyone, signed in or not (logos on the login page, favicons).
    /// Only media that a brand uses is served; anything else is not found.
    /// </summary>
    public async Task<MediaContent> OpenPublicImageAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var file = await brands.FindBrandImageAsync(mediaId, cancellationToken) ?? throw new NotFoundException("Brand image", mediaId);
        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        return new MediaContent(content, file.ContentType, file.FileName, content.CanSeek ? content.Length : 0);
    }

    private async Task<BrandResponse> SaveAsync(Brand? brand, Guid? coachId, BrandInput input, CancellationToken cancellationToken)
    {
        if (brand is null)
        {
            brand = DomainValidation.Run(() => Brand.Create(coachId, input.BrandName, Now), "brandName");
            Apply(brand, input);
            await brands.AddAsync(brand, cancellationToken);
        }
        else
        {
            Apply(brand, input);
            await brands.SaveChangesAsync(cancellationToken);
        }
        return Map(brand, coachId, brand.BrandName);
    }

    private void Apply(Brand brand, BrandInput input) =>
        DomainValidation.Run(() => brand.Update(
            input.BrandName, input.PrimaryColor, input.SecondaryColor, input.LoginHeadline, input.LoginMessage,
            input.DashboardWelcome, input.EmailHeader, input.EmailFooter, Now), "brand");

    private async Task<Brand> CreateAsync(Guid? coachId, CancellationToken cancellationToken)
    {
        var name = coachId is { } id ? await CoachNameAsync(id, cancellationToken) : BrandDefaults.BrandName;
        var brand = Brand.Create(coachId, name, Now);
        await brands.AddAsync(brand, cancellationToken);
        return brand;
    }

    private async Task ReplaceImageAsync(
        Brand brand, BrandImage image, BrandImageUpload upload, MediaOwner owner, CancellationToken cancellationToken)
    {
        if (!ImageTypes.Contains(upload.ContentType))
            throw Validation("file", "Use a PNG, JPEG, WebP or GIF image.");
        if (upload.SizeBytes > MaximumImageBytes)
            throw Validation("file", "Brand images may be at most 1 MB.");

        var stored = await media.UploadAsync(upload.Content, upload.FileName, upload.ContentType, upload.SizeBytes, owner, cancellationToken);
        var previous = brand.ImageOf(image);
        brand.SetImage(image, stored.Id, Now);
        await brands.SaveChangesAsync(cancellationToken);
        if (previous is { } previousId) await DeleteQuietlyAsync(previousId, cancellationToken);
    }

    private async Task RemoveImageAsync(Brand brand, BrandImage image, CancellationToken cancellationToken)
    {
        if (brand.ImageOf(image) is not { } previous) return;
        brand.SetImage(image, null, Now);
        await brands.SaveChangesAsync(cancellationToken);
        await DeleteQuietlyAsync(previous, cancellationToken);
    }

    private async Task DeleteQuietlyAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        try
        {
            await media.DeleteAsync(mediaId, cancellationToken);
        }
        catch (Exception exception) when (exception is NotFoundException or ConflictException)
        {
            // Already gone, or still referenced elsewhere: leave it.
        }
    }

    private Task<Guid> CoachAsync(Guid? coachId, CancellationToken cancellationToken) =>
        ownership.ForNewRecordAsync(coachId, cancellationToken);

    private async Task<string> CoachNameAsync(Guid coachId, CancellationToken cancellationToken) =>
        (await coaches.FindAsync(coachId, cancellationToken))?.DisplayName ?? BrandDefaults.BrandName;

    private static BrandResponse Map(Brand? brand, Guid? coachId, string defaultName) => brand is null
        ? new(coachId, false, defaultName, null, null, null, null, null, null, null, null, null, null, null, null, null)
        : new(brand.CoachId, true, brand.BrandName, brand.PrimaryColor, brand.SecondaryColor, brand.LogoMediaId,
            brand.FaviconMediaId, brand.DefaultAvatarMediaId, brand.LoginHeadline, brand.LoginMessage,
            brand.DashboardWelcome, brand.EmailHeader, brand.EmailFooter, brand.Subdomain, brand.CustomDomain, brand.UpdatedAt);

    private DateTimeOffset Now => timeProvider.GetUtcNow();

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
