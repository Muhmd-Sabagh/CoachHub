using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Settings;

/// <summary>The brand images an Administrator or coach can set (§63).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrandImage>))]
public enum BrandImage
{
    Logo,
    Favicon,
    DefaultAvatar
}

/// <summary>
/// White-label branding (D1, §63). One platform brand (no coach) is the default; each coach may
/// have their own, and clients see their coach's. Empty optional fields fall back to the
/// platform brand. Subdomain and custom domain are stored now so hosts can resolve a coach later.
/// </summary>
public sealed partial class Brand : Entity, IOptionalTenantOwned
{
    public const int MaxTextLength = 500;

    private Brand() { }

    /// <summary>The owning coach; null for the platform brand.</summary>
    public Guid? CoachId { get; private set; }
    public string BrandName { get; private set; } = string.Empty;

    /// <summary>"#RRGGBB"; null uses the platform color.</summary>
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public Guid? LogoMediaId { get; private set; }
    public Guid? FaviconMediaId { get; private set; }
    public Guid? DefaultAvatarMediaId { get; private set; }
    public string? LoginHeadline { get; private set; }
    public string? LoginMessage { get; private set; }
    public string? DashboardWelcome { get; private set; }
    public string? EmailHeader { get; private set; }
    public string? EmailFooter { get; private set; }

    /// <summary>Lower-case DNS label, e.g. "coacha" for coacha.platform.com. Coach brands only.</summary>
    public string? Subdomain { get; private set; }

    /// <summary>Lower-case host name, e.g. "www.coacha.com". Coach brands only.</summary>
    public string? CustomDomain { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsPlatformBrand => CoachId is null;

    public static Brand Create(Guid? coachId, string brandName, DateTimeOffset now)
    {
        var brand = new Brand { CoachId = TenantKey.Optional(coachId) };
        brand.Update(brandName, null, null, null, null, null, null, null, now);
        return brand;
    }

    public void Update(
        string brandName,
        string? primaryColor,
        string? secondaryColor,
        string? loginHeadline,
        string? loginMessage,
        string? dashboardWelcome,
        string? emailHeader,
        string? emailFooter,
        DateTimeOffset now)
    {
        BrandName = Required(brandName, 100, nameof(brandName));
        PrimaryColor = Color(primaryColor, nameof(primaryColor));
        SecondaryColor = Color(secondaryColor, nameof(secondaryColor));
        LoginHeadline = Optional(loginHeadline, 150, nameof(loginHeadline));
        LoginMessage = Optional(loginMessage, MaxTextLength, nameof(loginMessage));
        DashboardWelcome = Optional(dashboardWelcome, MaxTextLength, nameof(dashboardWelcome));
        EmailHeader = Optional(emailHeader, MaxTextLength, nameof(emailHeader));
        EmailFooter = Optional(emailFooter, MaxTextLength, nameof(emailFooter));
        UpdatedAt = now;
    }

    /// <summary>Hosts are platform infrastructure: only coach brands have them, set by an Administrator.</summary>
    public void SetHosts(string? subdomain, string? customDomain, DateTimeOffset now)
    {
        var label = Normalize(subdomain);
        var host = Normalize(customDomain);
        if ((label is not null || host is not null) && IsPlatformBrand)
            throw new ArgumentException("The platform brand has no subdomain or custom domain.");
        if (label is not null && (label.Length > 63 || !SubdomainPattern().IsMatch(label)))
            throw new ArgumentException("Use letters, digits and hyphens, not at the start or end.", nameof(subdomain));
        if (host is not null && (host.Length > 253 || !HostPattern().IsMatch(host)))
            throw new ArgumentException("Use a host name such as www.example.com.", nameof(customDomain));
        Subdomain = label;
        CustomDomain = host;
        UpdatedAt = now;
    }

    public void SetImage(BrandImage image, Guid? mediaId, DateTimeOffset now)
    {
        if (mediaId == Guid.Empty) throw new ArgumentException("A media identifier cannot be empty.", nameof(mediaId));
        switch (image)
        {
            case BrandImage.Logo: LogoMediaId = mediaId; break;
            case BrandImage.Favicon: FaviconMediaId = mediaId; break;
            case BrandImage.DefaultAvatar: DefaultAvatarMediaId = mediaId; break;
            default: throw new ArgumentOutOfRangeException(nameof(image));
        }
        UpdatedAt = now;
    }

    public Guid? ImageOf(BrandImage image) => image switch
    {
        BrandImage.Logo => LogoMediaId,
        BrandImage.Favicon => FaviconMediaId,
        BrandImage.DefaultAvatar => DefaultAvatarMediaId,
        _ => throw new ArgumentOutOfRangeException(nameof(image))
    };

    private static string? Color(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var color = value.Trim().ToLowerInvariant();
        return ColorPattern().IsMatch(color) ? color : throw new ArgumentException("Use a color such as #465fff.", name);
    }

    private static string Required(string value, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : throw new ArgumentOutOfRangeException(name);
    }

    private static string? Optional(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, max, name);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.').ToLowerInvariant();

    [GeneratedRegex("^#[0-9a-f]{6}$")]
    private static partial Regex ColorPattern();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex SubdomainPattern();

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$")]
    private static partial Regex HostPattern();
}
