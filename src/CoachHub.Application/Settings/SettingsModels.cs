using CoachHub.Domain.Progress;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Settings;

// ---- Branding (D1, §63) ----

/// <summary>A brand as stored, for editing. Empty fields fall back to the platform brand.</summary>
public sealed record BrandResponse(
    Guid? CoachId,
    bool IsSaved,
    string BrandName,
    string? PrimaryColor,
    string? SecondaryColor,
    Guid? LogoMediaId,
    Guid? FaviconMediaId,
    Guid? DefaultAvatarMediaId,
    string? LoginHeadline,
    string? LoginMessage,
    string? DashboardWelcome,
    string? EmailHeader,
    string? EmailFooter,
    string? Subdomain,
    string? CustomDomain,
    DateTimeOffset? UpdatedAt);

public sealed record BrandInput(
    string BrandName,
    string? PrimaryColor,
    string? SecondaryColor,
    string? LoginHeadline,
    string? LoginMessage,
    string? DashboardWelcome,
    string? EmailHeader,
    string? EmailFooter);

/// <summary>Future host resolution (§63): coacha.platform.com or www.coacha.com.</summary>
public sealed record BrandHostsInput(string? Subdomain, string? CustomDomain);

/// <summary>The brand a user sees: the coach's brand over the platform brand over built-in defaults.</summary>
public sealed record EffectiveBrand(
    Guid? CoachId,
    string BrandName,
    string PrimaryColor,
    string SecondaryColor,
    Guid? LogoMediaId,
    Guid? FaviconMediaId,
    Guid? DefaultAvatarMediaId,
    string? LoginHeadline,
    string? LoginMessage,
    string? DashboardWelcome,
    string? EmailHeader,
    string? EmailFooter);

public sealed record BrandImageUpload(Stream Content, string FileName, string ContentType, long SizeBytes);

/// <summary>A public brand image's stored content, located without the caller's tenant filter.</summary>
public sealed record BrandImageFile(string StorageKey, string ContentType, string FileName);

public interface IBrandRepository
{
    /// <summary>Brands are read across tenants on purpose: the platform brand and a coach's brand are public.</summary>
    Task<Brand?> FindPlatformAsync(CancellationToken cancellationToken);

    Task<Brand?> FindForCoachAsync(Guid coachId, CancellationToken cancellationToken);

    /// <summary>The coach whose brand owns this subdomain or custom domain.</summary>
    Task<Guid?> FindCoachByHostAsync(string? subdomain, string? customDomain, CancellationToken cancellationToken);

    Task<bool> HostTakenAsync(string? subdomain, string? customDomain, Guid exceptBrandId, CancellationToken cancellationToken);

    /// <summary>The stored file of a media item, only when some brand uses it as an image.</summary>
    Task<BrandImageFile?> FindBrandImageAsync(Guid mediaId, CancellationToken cancellationToken);

    Task AddAsync(Brand brand, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Which coach's branding applies to a request (D1, §63).</summary>
public interface ITenantResolver
{
    /// <summary>
    /// The signed-in user's coach; otherwise the coach that owns <paramref name="host"/> as a
    /// subdomain of the platform domain or as a custom domain; otherwise null (platform).
    /// </summary>
    Task<Guid?> ResolveCoachAsync(string? host, CancellationToken cancellationToken);
}

/// <summary>Bound from <c>Branding</c>.</summary>
public sealed class BrandingOptions
{
    public const string SectionName = "Branding";

    /// <summary>The platform's own domain, e.g. "coachhub.example"; coaches' subdomains sit under it.</summary>
    public string? PlatformDomain { get; init; }
}

// ---- Settings (§49) ----

public sealed record PlatformSettingsResponse(
    string DefaultLanguage,
    UnitSystem Units,
    bool EmailNotificationsEnabled,
    bool WhatsAppNotificationsEnabled,
    string? EmailSenderName,
    DateTimeOffset? UpdatedAt);

public sealed record PlatformSettingsInput(
    string DefaultLanguage,
    UnitSystem Units,
    bool EmailNotificationsEnabled,
    bool WhatsAppNotificationsEnabled,
    string? EmailSenderName);

public sealed record CoachSettingsResponse(
    Guid CoachId,
    bool IsSaved,
    ActivityFactors ActivityFactors,
    decimal DeficitPercent,
    decimal SurplusPercent,
    decimal ProteinGramsPerKg,
    decimal FatGramsPerKg,
    MacroMode DefaultMacroMode,
    AdherenceWeights AdherenceWeights,
    FoodReplacementRule ReplacementRule,
    DateTimeOffset? UpdatedAt,
    OneRepMaxFormula OneRepMaxFormula = OneRepMaxFormula.Epley,
    decimal SecondaryMuscleWeight = 0m,
    LifestyleTargetValues? DefaultTargets = null);

public sealed record CoachSettingsInput(
    ActivityFactors ActivityFactors,
    decimal DeficitPercent,
    decimal SurplusPercent,
    decimal ProteinGramsPerKg,
    decimal FatGramsPerKg,
    MacroMode DefaultMacroMode,
    AdherenceWeights AdherenceWeights,
    FoodReplacementRule ReplacementRule,
    OneRepMaxFormula OneRepMaxFormula = OneRepMaxFormula.Epley,
    decimal SecondaryMuscleWeight = 0m,
    LifestyleTargetValues? DefaultTargets = null);

/// <summary>What every signed-in screen needs: the brand name to show, units and default language.</summary>
public sealed record WorkspaceSettingsResponse(string ProductName, string? CoachName, UnitSystem Units, string DefaultLanguage);

public interface ISettingsRepository
{
    Task<PlatformSettings?> FindPlatformAsync(CancellationToken cancellationToken);
    Task<CoachSettings?> FindCoachAsync(Guid coachId, CancellationToken cancellationToken);
    void Add(object settings);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal static class BrandDefaults
{
    public const string BrandName = "CoachHub";
    public const string PrimaryColor = "#465fff";
    public const string SecondaryColor = "#12b76a";
}

/// <summary>One access rule (§57): a feature's access for a subscription status other than Active.</summary>
public sealed record SubscriptionAccessRuleItem(
    CoachHub.Domain.Clients.SubscriptionStatus Status,
    ClientFeature Feature,
    FeatureAccess Access,
    bool IsDefault = false);

/// <summary>
/// The subscription settings (§47, §57): duration presets in months, and access per status and feature. The
/// response lists every cell, marking those that use the default.
/// </summary>
public sealed record SubscriptionSettingsResponse(IReadOnlyList<int> DurationPresets, IReadOnlyList<SubscriptionAccessRuleItem> Rules);

public sealed record SubscriptionSettingsInput(IReadOnlyList<int> DurationPresets, IReadOnlyList<SubscriptionAccessRuleItem> Rules);
