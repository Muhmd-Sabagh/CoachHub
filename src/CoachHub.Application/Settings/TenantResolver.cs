using CoachHub.Application.Common.Tenancy;

namespace CoachHub.Application.Settings;

/// <summary>
/// Resolves the coach whose branding applies. A signed-in coach or client always gets their own
/// coach; before sign-in the host decides: a subdomain of the platform domain or a coach's custom
/// domain. Anything else is the platform.
/// </summary>
public sealed class TenantResolver(ICurrentUser currentUser, IBrandRepository brands, BrandingOptions options) : ITenantResolver
{
    public async Task<Guid?> ResolveCoachAsync(string? host, CancellationToken cancellationToken)
    {
        if (currentUser.Scope is TenantScope.Coach or TenantScope.Client && currentUser.CoachId is { } coachId)
            return coachId;

        var (subdomain, customDomain) = Classify(host, options.PlatformDomain);
        return subdomain is null && customDomain is null
            ? null
            : await brands.FindCoachByHostAsync(subdomain, customDomain, cancellationToken);
    }

    /// <summary>
    /// Splits a host into a coach subdomain (one label directly under the platform domain) or a
    /// custom domain (any other host). The platform domain itself, "www." on it, localhost and IP
    /// addresses resolve to nothing.
    /// </summary>
    public static (string? Subdomain, string? CustomDomain) Classify(string? host, string? platformDomain)
    {
        var name = Normalize(host);
        if (name is null || name == "localhost" || System.Net.IPAddress.TryParse(name, out _)) return (null, null);

        var platform = Normalize(platformDomain);
        if (platform is not null)
        {
            if (name == platform || name == "www." + platform) return (null, null);
            if (name.EndsWith("." + platform, StringComparison.Ordinal))
            {
                var label = name[..^(platform.Length + 1)];
                return label.Contains('.') ? (null, null) : (label, null);
            }
        }
        return (null, name);
    }

    private static string? Normalize(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var value = host.Trim().TrimEnd('.').ToLowerInvariant();
        // Drop a port, but leave IPv6 literals alone.
        var colon = value.LastIndexOf(':');
        if (colon > 0 && !value.Contains(']') && value.IndexOf(':') == colon) value = value[..colon];
        return value.Length is 0 or > 253 ? null : value;
    }
}
