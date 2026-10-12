using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.API.Settings;
using CoachHub.Application.Clients;
using CoachHub.Application.Communications;
using CoachHub.Application.Settings;
using CoachHub.Domain.Communications;
using CoachHub.Domain.Settings;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// White-label branding (D1, §63) and settings (§49) on SQL Server: each coach's clients see
/// their coach's brand, hosts select a brand before sign-in, only brand images are public, and
/// settings are tenant-isolated.
/// </summary>
public sealed class BrandingAndSettingsTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Clients_see_their_coachs_brand_over_the_platform_brand()
    {
        var admin = factory.CreateAdminClient();
        (await admin.PutAsJsonAsync("/api/v1/branding/platform", Input("Platform brand", "#101010", "#202020"))).EnsureSuccessStatusCode();
        var coachIdA = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachIdA);
        (await coachA.PutAsJsonAsync("/api/v1/branding/coach", Input("Coach A Fitness", "#aa0000", null) with
        {
            DashboardWelcome = "Train with A"
        })).EnsureSuccessStatusCode();
        var clientOfA = await CreateClientAsync(coachA);
        var coachB = await factory.CreateCoachClientAsync();
        var clientOfB = await CreateClientAsync(coachB);

        var seenByA = await factory.CreateClientPortalClient(coachIdA, clientOfA.Id).GetFromJsonAsync<BrandView>("/api/v1/branding");
        var seenByB = await factory.CreateClientPortalClient(clientOfB.CoachId, clientOfB.Id).GetFromJsonAsync<BrandView>("/api/v1/branding");

        Assert.Equal("Coach A Fitness", seenByA!.BrandName);
        Assert.Equal("#aa0000", seenByA.PrimaryColor);
        Assert.Equal("#202020", seenByA.SecondaryColor);
        Assert.Equal("Train with A", seenByA.DashboardWelcome);
        Assert.Equal("Platform brand", seenByB!.BrandName);
        Assert.Equal("Coach A Fitness",
            (await coachA.GetFromJsonAsync<WorkspaceSettingsResponse>("/api/v1/settings"))!.ProductName);
    }

    [Fact]
    public async Task Coaches_edit_only_their_own_brand()
    {
        var coachIdA = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachIdA);
        var coachB = await factory.CreateCoachClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await coachB.PutAsJsonAsync("/api/v1/branding/platform", Input("Hijack", null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await coachB.PutAsJsonAsync($"/api/v1/branding/coach?coachId={coachIdA}", Input("Hijack", null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await coachB.PutAsJsonAsync($"/api/v1/branding/coach/hosts?coachId={coachIdA}", new BrandHostsInput("hijack", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await coachA.PutAsJsonAsync("/api/v1/branding/coach", Input("Coach A", "orange", null))).StatusCode);

        var unsaved = await coachA.GetFromJsonAsync<BrandResponse>("/api/v1/branding/coach");
        Assert.False(unsaved!.IsSaved);
    }

    [Fact]
    public async Task A_custom_domain_selects_its_coachs_brand_before_sign_in_and_hosts_are_unique()
    {
        var admin = factory.CreateAdminClient();
        var coachIdA = await factory.CreateCoachAsync();
        var coachIdB = await factory.CreateCoachAsync();
        var domain = $"www.coach-{Guid.NewGuid():N}.com";
        (await factory.CreateCoachClient(coachIdA).PutAsJsonAsync("/api/v1/branding/coach", Input("Domain coach", "#00aa00", null) with
        {
            LoginHeadline = "Welcome to Domain coach"
        })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PutAsJsonAsync($"/api/v1/branding/coach/hosts?coachId={coachIdA}", new BrandHostsInput(null, domain))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PutAsJsonAsync($"/api/v1/branding/coach/hosts?coachId={coachIdB}", new BrandHostsInput(null, domain))).StatusCode);

        using var anonymous = factory.CreateClient();
        var branded = await anonymous.GetFromJsonAsync<PublicBrandResponse>($"/api/v1/branding/public?host={domain}");
        var platform = await anonymous.GetFromJsonAsync<PublicBrandResponse>("/api/v1/branding/public?host=unknown.example.org");
        Assert.Equal("Domain coach", branded!.BrandName);
        Assert.Equal("Welcome to Domain coach", branded.LoginHeadline);
        Assert.NotEqual("Domain coach", platform!.BrandName);
    }

    [Fact]
    public async Task Only_brand_images_are_served_anonymously_and_replaced_images_are_removed()
    {
        var coachIdA = await factory.CreateCoachAsync();
        // A real account: uploads record their uploader, which must exist.
        var coachA = (await factory.CreateSignedInAccountAsync(Application.Auth.AuthRoles.Coach, coachIdA)).Http;
        var client = await CreateClientAsync(coachA);
        var clientAccount = await factory.CreateSignedInAccountAsync(Application.Auth.AuthRoles.Client, client.CoachId, client.Id);
        var clientPhoto = await (await MeEndpointTests.UploadAvatarAsync(clientAccount.Http, MeEndpointTests.Png, "image/png"))
            .Content.ReadFromJsonAsync<Application.Media.MediaMetadata>();

        var first = await UploadLogoAsync(coachA);
        var second = await UploadLogoAsync(coachA);

        using var anonymous = factory.CreateClient();
        var served = await anonymous.GetAsync($"/api/v1/branding/assets/{second.LogoMediaId}");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/branding/assets/{first.LogoMediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/branding/assets/{clientPhoto!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await factory.CreateClientPortalClient(client.CoachId, client.Id).GetAsync($"/api/v1/media/{second.LogoMediaId}")).StatusCode);

        var removed = await coachA.DeleteAsync("/api/v1/branding/coach/images/Logo");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/branding/assets/{second.LogoMediaId}")).StatusCode);
    }

    [Fact]
    public async Task Coach_settings_default_until_saved_validate_and_stay_in_their_tenant()
    {
        var coachIdA = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachIdA);
        var coachB = await factory.CreateCoachClientAsync();

        var defaults = await coachA.GetFromJsonAsync<CoachSettingsResponse>("/api/v1/settings/coach");
        Assert.False(defaults!.IsSaved);
        Assert.Equal(20m, defaults.DeficitPercent);

        var invalid = await coachA.PutAsJsonAsync("/api/v1/settings/coach", Settings(new AdherenceWeights(50, 50, 50, 0, 0, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("adherenceWeights", await invalid.Content.ReadAsStringAsync());

        var saved = await coachA.PutAsJsonAsync("/api/v1/settings/coach", Settings(new AdherenceWeights(40, 30, 10, 10, 5, 5)));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(25m, (await coachA.GetFromJsonAsync<CoachSettingsResponse>("/api/v1/settings/coach"))!.DeficitPercent);

        Assert.Equal(HttpStatusCode.Forbidden, (await coachB.GetAsync($"/api/v1/settings/coach?coachId={coachIdA}")).StatusCode);
        Assert.Equal(25m, (await factory.CreateAdminClient()
            .GetFromJsonAsync<CoachSettingsResponse>($"/api/v1/settings/coach?coachId={coachIdA}"))!.DeficitPercent);
        Assert.Equal(HttpStatusCode.Forbidden, (await coachA.GetAsync("/api/v1/settings/platform")).StatusCode);
    }

    [Fact]
    public async Task Turning_email_off_blocks_coach_emails_but_not_account_emails()
    {
        var admin = factory.CreateAdminClient();
        var coach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);
        try
        {
            (await admin.PutAsJsonAsync("/api/v1/settings/platform",
                new PlatformSettingsInput("en", UnitSystem.Metric, false, true, null))).EnsureSuccessStatusCode();

            var email = await coach.PostAsJsonAsync("/api/v1/notifications",
                new NotificationInput(client.Id, NotificationChannel.Email, "client@example.test", "Hello", "Body", null));
            var whatsApp = await coach.PostAsJsonAsync("/api/v1/notifications",
                new NotificationInput(client.Id, NotificationChannel.WhatsApp, "+201000000000", "Hello", "Body", null));

            Assert.Equal(HttpStatusCode.Conflict, email.StatusCode);
            Assert.Equal(HttpStatusCode.Created, whatsApp.StatusCode);
            Assert.Equal(UnitSystem.Metric, (await coach.GetFromJsonAsync<WorkspaceSettingsResponse>("/api/v1/settings"))!.Units);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/v1/settings/platform",
                new PlatformSettingsInput("en", UnitSystem.Metric, true, true, null))).EnsureSuccessStatusCode();
        }
    }

    private static BrandInput Input(string name, string? primary, string? secondary) =>
        new(name, primary, secondary, null, null, null, null, null);

    private static CoachSettingsInput Settings(AdherenceWeights weights) => new(
        new ActivityFactors(1.2m, 1.375m, 1.55m, 1.725m, 1.9m), 25m, 10m, 2m, 0.8m, MacroMode.GramsPerKg, weights,
        FoodReplacementRule.Calories);

    private static async Task<BrandResponse> UploadLogoAsync(HttpClient coach)
    {
        var file = new ByteArrayContent(MeEndpointTests.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var response = await coach.PutAsync("/api/v1/branding/coach/images/Logo",
            new MultipartFormDataContent { { file, "file", "logo.png" } });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BrandResponse>())!;
    }

    private static async Task<ClientResponse> CreateClientAsync(HttpClient coach)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Brand client " + Guid.NewGuid().ToString("N")[..6], null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }
}
