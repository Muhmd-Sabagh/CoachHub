using System.Reflection;

namespace CoachHub.API.Common.Hosting;

/// <summary>
/// The oldest ASP.NET Core runtime the API runs on (P23). SignalR in 10.0.0–10.0.12 has a denial-of-service advisory
/// (CVE-2026-70159, fixed in 10.0.13; see P14), so staging and production refuse to start on an older runtime.
/// Development and Testing only log it, so a developer machine still runs.
/// </summary>
public static class RuntimeRequirement
{
    public static readonly Version Minimum = new(10, 0, 13);

    /// <summary>The ASP.NET Core shared framework version the process runs on, from its informational version.</summary>
    public static Version Current
    {
        get
        {
            var text = typeof(WebApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(WebApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            return Parse(text);
        }
    }

    /// <summary>"10.0.15+abc" or "10.0.15-servicing.1" → 10.0.15; anything unreadable → 0.0.0.</summary>
    public static Version Parse(string text)
    {
        var core = text.Split('+', '-')[0];
        return Version.TryParse(core, out var version) ? new Version(version.Major, version.Minor, Math.Max(version.Build, 0)) : new Version(0, 0, 0);
    }

    public static bool IsSupported(Version runtime) => runtime >= Minimum;

    /// <summary>Stops startup outside Development and Testing on an unsupported runtime.</summary>
    public static void Enforce(IHostEnvironment environment, ILogger logger, Version? runtime = null)
    {
        var current = runtime ?? Current;
        if (IsSupported(current)) return;
        var message = $"ASP.NET Core {current} is older than the required {Minimum} (CVE-2026-70159). Install the latest 10.0 runtime.";
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            logger.LogWarning("{Message}", message);
            return;
        }
        throw new InvalidOperationException(message);
    }
}
