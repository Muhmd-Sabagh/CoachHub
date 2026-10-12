using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Media;
using CoachHub.Application.Settings;
using CoachHub.Domain.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoachHub.API.Settings;

/// <summary>
/// White-label branding (D1, §63). Two actions are anonymous because the login page is branded
/// before anyone signs in: the public brand for a host, and the brand images themselves. Both
/// return only what is already meant to be public.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AnyRole)]
[Route(ApiRoutes.V1 + "/branding")]
public sealed class BrandingController(BrandingService branding) : ControllerBase
{
    /// <summary>The brand for the login page: the host's coach, otherwise the platform.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [EnableRateLimiting("branding")]
    public async Task<PublicBrandResponse> Public([FromQuery] string? host, CancellationToken cancellationToken)
    {
        var brand = await branding.ForRequestAsync(host, cancellationToken);
        return new PublicBrandResponse(
            brand.BrandName, brand.PrimaryColor, brand.SecondaryColor, AssetUrl(brand.LogoMediaId),
            AssetUrl(brand.FaviconMediaId), brand.LoginHeadline, brand.LoginMessage);
    }

    /// <summary>A brand image (logo, favicon, default avatar). Other media is never served here.</summary>
    [HttpGet("assets/{id:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting("branding")]
    public async Task<IActionResult> Asset(Guid id, CancellationToken cancellationToken)
    {
        var image = await branding.OpenPublicImageAsync(id, cancellationToken);
        Response.Headers.CacheControl = "public, max-age=3600";
        return File(image.Content, image.ContentType);
    }

    /// <summary>The brand the signed-in user sees: their coach's, or the platform's.</summary>
    [HttpGet]
    public async Task<BrandView> Current([FromQuery] string? host, CancellationToken cancellationToken) =>
        View(await branding.ForRequestAsync(host, cancellationToken));

    [HttpGet("platform")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<BrandResponse> Platform(CancellationToken cancellationToken) => branding.GetPlatformAsync(cancellationToken);

    [HttpPut("platform")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<BrandResponse> UpdatePlatform(BrandInput input, CancellationToken cancellationToken) =>
        branding.UpdatePlatformAsync(input, cancellationToken);

    [HttpPut("platform/images/{image}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<BrandResponse> SetPlatformImage(BrandImage image, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return await branding.SetPlatformImageAsync(image, Upload(file, content), cancellationToken);
    }

    [HttpDelete("platform/images/{image}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<BrandResponse> RemovePlatformImage(BrandImage image, CancellationToken cancellationToken) =>
        branding.RemovePlatformImageAsync(image, cancellationToken);

    /// <summary>The caller's own brand; an Administrator passes <paramref name="coachId"/>.</summary>
    [HttpGet("coach")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    public Task<BrandResponse> Coach([FromQuery] Guid? coachId, CancellationToken cancellationToken) =>
        branding.GetCoachAsync(coachId, cancellationToken);

    [HttpPut("coach")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    public Task<BrandResponse> UpdateCoach([FromQuery] Guid? coachId, BrandInput input, CancellationToken cancellationToken) =>
        branding.UpdateCoachAsync(coachId, input, cancellationToken);

    /// <summary>Subdomain and custom domain: platform infrastructure, so Administrators only.</summary>
    [HttpPut("coach/hosts")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<BrandResponse> SetHosts([FromQuery] Guid coachId, BrandHostsInput input, CancellationToken cancellationToken) =>
        branding.SetHostsAsync(coachId, input, cancellationToken);

    [HttpPut("coach/images/{image}")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<BrandResponse> SetCoachImage(
        BrandImage image, [FromQuery] Guid? coachId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return await branding.SetCoachImageAsync(coachId, image, Upload(file, content), cancellationToken);
    }

    [HttpDelete("coach/images/{image}")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    public Task<BrandResponse> RemoveCoachImage(BrandImage image, [FromQuery] Guid? coachId, CancellationToken cancellationToken) =>
        branding.RemoveCoachImageAsync(coachId, image, cancellationToken);

    private static BrandImageUpload Upload(IFormFile file, Stream content) =>
        new(content, file.FileName, file.ContentType, file.Length);

    private static string? AssetUrl(Guid? mediaId) =>
        mediaId is { } id ? $"/{ApiRoutes.V1}/branding/assets/{id}" : null;

    private static BrandView View(EffectiveBrand brand) => new(
        brand.CoachId, brand.BrandName, brand.PrimaryColor, brand.SecondaryColor, AssetUrl(brand.LogoMediaId),
        AssetUrl(brand.FaviconMediaId), AssetUrl(brand.DefaultAvatarMediaId), brand.LoginHeadline, brand.LoginMessage,
        brand.DashboardWelcome);
}

/// <summary>Only what the login page shows.</summary>
public sealed record PublicBrandResponse(
    string BrandName,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl,
    string? FaviconUrl,
    string? LoginHeadline,
    string? LoginMessage);

public sealed record BrandView(
    Guid? CoachId,
    string BrandName,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl,
    string? FaviconUrl,
    string? DefaultAvatarUrl,
    string? LoginHeadline,
    string? LoginMessage,
    string? DashboardWelcome);
