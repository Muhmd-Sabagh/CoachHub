using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace CoachHub.API.Common.Hosting;

/// <summary>
/// Trusted reverse proxies (P18). Behind a load balancer or gateway every request arrives from the proxy's address, so
/// the per-address rate limits (sign-in, session refresh) would be shared by every user, and HTTPS would look like HTTP.
/// The client address and scheme are taken from <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>, but only from the
/// proxies and networks listed under <c>ReverseProxy</c>; anything else could spoof them. Loopback stays trusted, as in
/// ASP.NET Core's default. Invalid entries fail start-up.
/// </summary>
public static class ReverseProxy
{
    public const string Section = "ReverseProxy";

    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var proxy in Values(configuration, "KnownProxies"))
        {
            if (!IPAddress.TryParse(proxy, out var address))
                throw new InvalidOperationException($"{Section}:KnownProxies has an invalid address: '{proxy}'.");
            options.KnownProxies.Add(address);
        }
        foreach (var network in Values(configuration, "KnownNetworks"))
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsed))
                throw new InvalidOperationException($"{Section}:KnownNetworks has an invalid CIDR range: '{network}'.");
            options.KnownIPNetworks.Add(parsed);
        }
        // One hop per trusted proxy in front of the API (a CDN in front of a load balancer is two).
        options.ForwardLimit = configuration.GetValue<int?>($"{Section}:ForwardLimit") ?? 1;
    }

    private static IEnumerable<string> Values(IConfiguration configuration, string key) =>
        (configuration.GetSection($"{Section}:{key}").Get<string[]>() ?? [])
            .Select(value => value.Trim())
            .Where(value => value.Length > 0);
}
