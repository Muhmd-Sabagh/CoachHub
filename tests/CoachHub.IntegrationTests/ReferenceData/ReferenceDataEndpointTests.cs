using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.ReferenceData;

public sealed class ReferenceDataEndpointTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    [Fact]
    public async Task Coach_reads_own_settings_and_manages_own_packages()
    {
        var coachName = "Coach " + Guid.NewGuid().ToString("N")[..8];
        var coachId = await factory.CreateCoachAsync(coachName);
        var client = factory.CreateCoachClient(coachId);

        var settings = await client.GetFromJsonAsync<CoachHub.Application.Settings.WorkspaceSettingsResponse>("/api/v1/settings");
        Assert.NotNull(settings);
        Assert.Equal("CoachHub", settings.ProductName);
        Assert.Equal(coachName, settings.CoachName);

        var create = await client.PostAsJsonAsync(
            "/api/v1/reference-data/packages",
            new PackageInput("Starter", null, "Entry package", true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PackageResponse>();
        Assert.NotNull(created);
        Assert.Null(created.NameAr);
        Assert.Equal(coachId, created.CoachId);

        var page = await client.GetFromJsonAsync<PagedResult<PackageResponse>>(
            "/api/v1/reference-data/packages?searchTerm=Starter&isActive=true&pageNumber=1&pageSize=10");
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/reference-data/packages/{created.Id}",
            new PackageInput("Starter", "البداية", "Entry package", false));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<PackageResponse>();
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.Equal("البداية", updated.NameAr);

        var delete = await client.DeleteAsync($"/api/v1/reference-data/packages/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/reference-data/packages/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Package_names_are_unique_per_coach_not_across_coaches()
    {
        var first = await factory.CreateCoachClientAsync();
        var second = await factory.CreateCoachClientAsync();
        var name = "Shared name " + Guid.NewGuid().ToString("N");

        Assert.Equal(HttpStatusCode.Created,
            (await first.PostAsJsonAsync("/api/v1/reference-data/packages", new PackageInput(name, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await second.PostAsJsonAsync("/api/v1/reference-data/packages", new PackageInput(name, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await first.PostAsJsonAsync("/api/v1/reference-data/packages", new PackageInput(name, null, null))).StatusCode);
    }

    [Fact]
    public async Task Coaches_read_platform_reference_data_but_only_administrators_change_it()
    {
        var coach = await factory.CreateCoachClientAsync();
        var admin = factory.CreateAdminClient();
        var code = "Q" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await coach.PostAsJsonAsync("/api/v1/reference-data/currencies", new CurrencyInput(code, "Coach currency", null))).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/reference-data/currencies", new CurrencyInput(code, "Platform currency", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var currency = await created.Content.ReadFromJsonAsync<CurrencyResponse>();

        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/api/v1/reference-data/currencies/{currency!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coach.DeleteAsync($"/api/v1/reference-data/currencies/{currency.Id}")).StatusCode);
    }

    [Fact]
    public async Task Administrator_must_name_the_coach_that_owns_a_package()
    {
        var admin = factory.CreateAdminClient();
        var coachId = await factory.CreateCoachAsync();

        var withoutCoach = await admin.PostAsJsonAsync(
            "/api/v1/reference-data/packages", new PackageInput("Admin package " + Guid.NewGuid().ToString("N"), null, null));
        Assert.Equal(HttpStatusCode.BadRequest, withoutCoach.StatusCode);

        var withCoach = await admin.PostAsJsonAsync(
            "/api/v1/reference-data/packages",
            new PackageInput("Admin package " + Guid.NewGuid().ToString("N"), null, null, CoachId: coachId));
        Assert.Equal(HttpStatusCode.Created, withCoach.StatusCode);
        Assert.Equal(coachId, (await withCoach.Content.ReadFromJsonAsync<PackageResponse>())!.CoachId);
    }

    [Fact]
    public async Task Reference_data_rejects_anonymous_access()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/reference-data/currencies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
