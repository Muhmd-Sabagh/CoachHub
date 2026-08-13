using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Auth;

public sealed class AuthenticationEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Bootstrap_administrator_can_login_and_access_protected_endpoint()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(CoachHubApiFactory.AdminEmail);
            Assert.NotNull(user);
            Assert.True(await userManager.CheckPasswordAsync(
                user,
                CoachHubApiFactory.AdminPassword));
        }

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = CoachHubApiFactory.AdminEmail,
                password = CoachHubApiFactory.AdminPassword
            },
            CancellationToken.None);

        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>(
            CancellationToken.None);

        Assert.NotNull(login);
        Assert.Contains(AuthRoles.Administrator, login.Roles);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var meResponse = await _client.GetAsync("/api/auth/me", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Administrator_endpoint_rejects_anonymous_requests()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync(
            "/api/auth/me",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_registration_endpoint_does_not_exist()
    {
        var response = await _client.PostAsync(
            "/api/auth/register",
            content: null,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
