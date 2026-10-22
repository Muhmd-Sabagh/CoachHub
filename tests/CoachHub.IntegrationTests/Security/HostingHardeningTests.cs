using System.Net;
using CoachHub.API.Common.Hosting;
using CoachHub.IntegrationTests.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace CoachHub.IntegrationTests.Security;

/// <summary>P18 hardening: response caching and trusted reverse proxies.</summary>
public sealed class HostingHardeningTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    [Theory]
    [InlineData("/api/v1/clients")]
    [InlineData("/api/v1/dashboard/admin")]
    [InlineData("/api/v1/coaches/missing")]
    public async Task Staff_responses_are_never_cached(string path)
    {
        using var admin = factory.CreateAdminClient();
        var response = await admin.GetAsync(path);

        Assert.True(response.Headers.CacheControl?.NoStore, $"{path} answered {response.StatusCode} without no-store");
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Refused_requests_are_not_cached_either()
    {
        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public void Forwarded_headers_are_trusted_only_from_configured_proxies()
    {
        var options = new ForwardedHeadersOptions();
        ReverseProxy.Configure(options, Configuration(new()
        {
            ["ReverseProxy:KnownProxies:0"] = "10.0.0.5",
            ["ReverseProxy:KnownNetworks:0"] = "172.16.0.0/12",
            ["ReverseProxy:ForwardLimit"] = "2"
        }));

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
        Assert.Contains(IPAddress.IPv6Loopback, options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("172.16.0.0/12"), options.KnownIPNetworks);
        Assert.Equal(2, options.ForwardLimit);
    }

    [Fact]
    public void Without_configuration_only_loopback_is_trusted()
    {
        var options = new ForwardedHeadersOptions();
        ReverseProxy.Configure(options, Configuration(new()));

        var defaults = new ForwardedHeadersOptions();
        Assert.Equal(defaults.KnownProxies, options.KnownProxies);
        Assert.Equal(defaults.KnownIPNetworks, options.KnownIPNetworks);
        Assert.Equal(1, options.ForwardLimit);
    }

    [Theory]
    [InlineData("ReverseProxy:KnownProxies:0", "not-an-address")]
    [InlineData("ReverseProxy:KnownNetworks:0", "10.0.0.0/99")]
    public void Invalid_proxy_settings_fail_start_up(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() =>
            ReverseProxy.Configure(new ForwardedHeadersOptions(), Configuration(new() { [key] = value })));

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
