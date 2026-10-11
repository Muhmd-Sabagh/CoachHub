using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Media;
using CoachHub.Domain.Coaches;
using CoachHub.IntegrationTests.Auth;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Me;

/// <summary>Self-service for every role (§2): profile, password, profile picture, sessions.</summary>
public sealed class MeEndpointTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    internal static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public async Task Profile_can_be_read_and_edited()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);

        var updated = await account.Http.PutAsJsonAsync("/api/v1/me/profile",
            new UpdateMyProfileInput("  Renamed Admin ", "+20 100 000 0000", "AR"));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var profile = await account.Http.GetFromJsonAsync<MyProfileResponse>("/api/v1/me/profile");
        Assert.Equal("Renamed Admin", profile!.DisplayName);
        Assert.Equal("+20 100 000 0000", profile.PhoneNumber);
        Assert.Equal("ar", profile.PreferredLanguage);
        Assert.Equal(AuthRoles.Administrator, profile.Role);
        Assert.Equal(account.Email, profile.Email);
    }

    [Fact]
    public async Task Invalid_profile_input_is_rejected_by_field()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);

        var response = await account.Http.PutAsJsonAsync("/api/v1/me/profile",
            new UpdateMyProfileInput("", "call me", "fr"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("displayName", body);
        Assert.Contains("phoneNumber", body);
        Assert.Contains("preferredLanguage", body);
    }

    [Fact]
    public async Task A_coach_renaming_themselves_renames_their_coach_record()
    {
        var coachId = await factory.CreateCoachAsync();
        var coach = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);

        await coach.Http.PutAsJsonAsync("/api/v1/me/profile", new UpdateMyProfileInput("Coach Renamed", null, null));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        Assert.Equal("Coach Renamed", (await db.Set<Coach>().SingleAsync(item => item.Id == coachId)).DisplayName);
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_signs_out_other_devices()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator, userAgent: "Laptop");
        await StartAnotherSessionAsync(account, "Phone");

        var wrong = await account.Http.PutAsJsonAsync("/api/v1/me/password",
            new ChangePasswordInput("not-the-password", "Another-Password!456"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("currentPassword", await wrong.Content.ReadAsStringAsync());

        var weak = await account.Http.PutAsJsonAsync("/api/v1/me/password",
            new ChangePasswordInput(TestIdentities.AccountPassword, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        Assert.Equal(2, (await SessionsAsync(account)).Count);
        var changed = await account.Http.PutAsJsonAsync("/api/v1/me/password",
            new ChangePasswordInput(TestIdentities.AccountPassword, "Another-Password!456"));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var remaining = Assert.Single(await SessionsAsync(account));
        Assert.True(remaining.IsCurrent);
        Assert.Equal("Laptop", remaining.UserAgent);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_signed_out_but_only_the_callers_own()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator, userAgent: "Laptop");
        await StartAnotherSessionAsync(account, "Phone");
        await StartAnotherSessionAsync(account, "Tablet");
        var stranger = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);

        var sessions = await SessionsAsync(account);
        Assert.Equal(3, sessions.Count);
        Assert.True(sessions[0].IsCurrent);
        var phone = sessions.Single(session => session.UserAgent == "Phone");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.DeleteAsync($"/api/v1/me/sessions/{phone.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await account.Http.DeleteAsync($"/api/v1/me/sessions/{phone.Id}")).StatusCode);
        Assert.Equal(2, (await SessionsAsync(account)).Count);

        Assert.Equal(HttpStatusCode.NoContent, (await account.Http.DeleteAsync("/api/v1/me/sessions")).StatusCode);
        Assert.True(Assert.Single(await SessionsAsync(account)).IsCurrent);
    }

    [Fact]
    public async Task A_profile_picture_can_be_set_replaced_and_removed()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);

        var first = await UploadAvatarAsync(account.Http, Png, "image/png");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstMedia = await first.Content.ReadFromJsonAsync<MediaMetadata>();
        Assert.Equal(firstMedia!.Id, (await ProfileAsync(account)).AvatarMediaId);
        Assert.Equal(HttpStatusCode.OK, (await account.Http.GetAsync($"/api/v1/media/{firstMedia.Id}")).StatusCode);

        var second = await (await UploadAvatarAsync(account.Http, Png, "image/png")).Content.ReadFromJsonAsync<MediaMetadata>();
        Assert.Equal(second!.Id, (await ProfileAsync(account)).AvatarMediaId);
        Assert.Equal(HttpStatusCode.NotFound, (await account.Http.GetAsync($"/api/v1/media/{firstMedia.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await account.Http.DeleteAsync("/api/v1/me/avatar")).StatusCode);
        Assert.Null((await ProfileAsync(account)).AvatarMediaId);
        Assert.Equal(HttpStatusCode.NotFound, (await account.Http.GetAsync($"/api/v1/media/{second.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_profile_picture_must_be_an_image()
    {
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);

        var pdf = await UploadAvatarAsync(account.Http, "%PDF-1.7"u8.ToArray(), "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, pdf.StatusCode);
        Assert.Null((await ProfileAsync(account)).AvatarMediaId);
    }

    [Fact]
    public async Task Self_service_requires_a_signed_in_account()
    {
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/me/sessions")).StatusCode);
    }

    internal static Task<HttpResponseMessage> UploadAvatarAsync(HttpClient http, byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", contentType == "application/pdf" ? "me.pdf" : "me.png" } };
        return http.PutAsync("/api/v1/me/avatar", form);
    }

    private static async Task<MyProfileResponse> ProfileAsync(SignedInAccount account) =>
        (await account.Http.GetFromJsonAsync<MyProfileResponse>("/api/v1/me/profile"))!;

    private static async Task<List<SessionResponse>> SessionsAsync(SignedInAccount account) =>
        (await account.Http.GetFromJsonAsync<List<SessionResponse>>("/api/v1/me/sessions"))!;

    private async Task StartAnotherSessionAsync(SignedInAccount account, string userAgent)
    {
        using var scope = factory.Services.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        await sessions.StartAsync(
            new AuthenticatedUser(account.UserId, account.Email, "Account", [AuthRoles.Administrator], null, null),
            new SessionClient(userAgent, null),
            CancellationToken.None);
    }
}
