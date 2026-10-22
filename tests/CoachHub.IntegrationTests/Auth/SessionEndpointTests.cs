using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CoachHub.IntegrationTests.Auth;

/// <summary>
/// Secure sessions over HTTP (§2, §52): the refresh cookie's attributes, rotation, replay
/// detection and server-side sign-out. The cookie is Secure, so tests carry it by hand.
/// </summary>
public sealed class SessionEndpointTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    private const string CookieName = "coachhub_refresh";

    [Fact]
    public async Task Login_sets_a_secure_http_only_strict_refresh_cookie_and_keeps_it_out_of_the_body()
    {
        var http = NewClient(factory);

        var response = await LoginAsync(http);

        var header = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(CookieName + "="));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", header, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(CookieValue(response)!, body);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(new JwtSecurityTokenHandler().ReadJwtToken(login!.AccessToken).Claims
            .SingleOrDefault(claim => claim.Type == AuthClaims.SessionId));
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_returns_a_working_access_token()
    {
        var http = NewClient(factory);
        var cookie = CookieValue(await LoginAsync(http))!;

        var refreshed = await RefreshAsync(http, cookie);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = CookieValue(refreshed);
        Assert.False(string.IsNullOrEmpty(rotated));
        Assert.NotEqual(cookie, rotated);
        var result = await refreshed.Content.ReadFromJsonAsync<LoginResult>();
        var me = NewClient(factory);
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_valid_cookie_is_unauthorized_and_clears_the_cookie()
    {
        var http = NewClient(factory);

        var missing = await http.PostAsync("/api/v1/auth/refresh", null);
        var unknown = await RefreshAsync(http, "not-a-session");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(string.Empty, CookieValue(unknown));
    }

    [Fact]
    public async Task Logout_ends_the_session_on_the_server()
    {
        var http = NewClient(factory);
        var response = await LoginAsync(http);
        var cookie = CookieValue(response)!;
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var logout = await http.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(string.Empty, CookieValue(logout));
        http.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(http, cookie)).StatusCode);
    }

    [Fact]
    public async Task Replaying_a_rotated_cookie_ends_the_session_for_every_holder()
    {
        // No grace period, so the second use of the first cookie is a replay.
        await using var strict = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Sessions:RotationGraceSeconds"] = "0"
            })));
        var http = NewClient(strict);
        var first = CookieValue(await LoginAsync(http))!;
        var second = CookieValue(await RefreshAsync(http, first))!;
        await Task.Delay(50);

        var replay = await RefreshAsync(http, first);
        var legitimate = await RefreshAsync(http, second);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient http) =>
        http.PostAsJsonAsync("/api/v1/auth/login", new
        {
            login = CoachHubApiFactory.AdminEmail,
            password = CoachHubApiFactory.AdminPassword
        });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient http, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        return http.SendAsync(request);
    }

    /// <summary>The refresh cookie's value in the response, "" when it is being cleared, null when not set.</summary>
    private static string? CookieValue(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers)) return null;
        var header = headers.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));
        if (header is null) return null;
        var value = header[(CookieName.Length + 1)..].Split(';')[0];
        return header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }
}
