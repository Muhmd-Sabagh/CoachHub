using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.API.Settings;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.ReferenceData;

public sealed class ReferenceDataEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;

    public ReferenceDataEndpointTests(CoachHubApiFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Administrator_can_read_settings_and_manage_packages()
    {
        await AuthenticateAsync();

        var settings = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");
        Assert.NotNull(settings);
        Assert.Equal("CoachHub", settings.ProductName);
        Assert.Equal("CoachHub Development Coach", settings.CoachName);

        var create = await _client.PostAsJsonAsync(
            "/api/reference-data/packages",
            new PackageInput("Starter", null, "Entry package", true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PackageResponse>();
        Assert.NotNull(created);
        Assert.Null(created.NameAr);

        var page = await _client.GetFromJsonAsync<PagedResult<PackageResponse>>(
            "/api/reference-data/packages?searchTerm=Starter&isActive=true&pageNumber=1&pageSize=10");
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);

        var update = await _client.PutAsJsonAsync(
            $"/api/reference-data/packages/{created.Id}",
            new PackageInput("Starter", "البداية", "Entry package", false));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<PackageResponse>();
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.Equal("البداية", updated.NameAr);

        var delete = await _client.DeleteAsync($"/api/reference-data/packages/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/reference-data/packages/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reference_data_rejects_anonymous_access()
    {
        var response = await _client.GetAsync("/api/reference-data/currencies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = CoachHubApiFactory.AdminEmail,
                password = CoachHubApiFactory.AdminPassword
            });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }
}