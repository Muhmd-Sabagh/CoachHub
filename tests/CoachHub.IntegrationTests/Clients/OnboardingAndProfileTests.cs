using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Domain.Settings;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using CoachHub.Domain.Nutrition;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Clients;

/// <summary>
/// Onboarding (§3, §2) and the structured profile (D3) on SQL Server: invitations, the client
/// choosing their own password, deactivation, profile validation and isolation, status (§39).
/// </summary>
public sealed partial class OnboardingAndProfileTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task An_invited_client_sets_their_own_password_once_and_can_sign_in()
    {
        var coach = await factory.CreateCoachClientAsync();
        var email = $"invitee.{Guid.NewGuid():N}@example.test";
        var created = await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Invitee", null, email, null, Invite: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var client = (await created.Content.ReadFromJsonAsync<ClientResponse>())!;

        var invited = await coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{client.Id}");
        Assert.Equal(ClientAccountState.Invited, invited!.Account.State);

        var (linkEmail, token) = await InvitationAsync(email);
        using var anonymous = factory.CreateClient();
        var accept = await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset/complete",
            new PasswordResetInput(linkEmail, token, "Client-Password!123"));
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        var reuse = await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset/complete",
            new PasswordResetInput(linkEmail, token, "Another-Password!123"));
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = email, password = "Client-Password!123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var active = await coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{client.Id}");
        Assert.Equal(ClientAccountState.Active, active!.Account.State);
        Assert.Equal(HttpStatusCode.Conflict, (await coach.PostAsync($"/api/v1/clients/{client.Id}/invite", null)).StatusCode);
    }

    [Fact]
    public async Task Invitations_need_an_email_and_one_account_per_email()
    {
        var coach = await factory.CreateCoachClientAsync();
        var email = $"shared.{Guid.NewGuid():N}@example.test";

        var withoutEmail = await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("No email", null, null, null, Invite: true));
        Assert.Equal(HttpStatusCode.BadRequest, withoutEmail.StatusCode);

        (await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("First", null, email, null, Invite: true))).EnsureSuccessStatusCode();
        var second = await (await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Second", null, email, null)))
            .Content.ReadFromJsonAsync<ClientResponse>();
        Assert.Equal(HttpStatusCode.Conflict, (await coach.PostAsync($"/api/v1/clients/{second!.Id}/invite", null)).StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_client_disables_their_login_and_ends_their_sessions()
    {
        var coach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Client, client.CoachId, client.Id);

        var deactivated = await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}", new ClientUpdateInput(
            client.Name, null, null, false));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        Assert.False((await db.Set<User>().SingleAsync(user => user.Id == account.UserId)).IsActive);
        Assert.Equal(SessionEndReason.AccountDisabled,
            (await db.Set<UserSession>().SingleAsync(session => session.UserId == account.UserId)).EndReason);
        Assert.Equal(ClientStatus.Inactive, (await coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{client.Id}"))!.Client.Status);
    }

    [Fact]
    public async Task The_profile_is_typed_validated_linked_to_catalogs_and_private_to_the_coach()
    {
        var admin = factory.CreateAdminClient();
        var equipment = await (await admin.PostAsJsonAsync("/api/v1/reference-data/equipment",
            new BilingualReferenceInput("Dumbbells " + Guid.NewGuid().ToString("N")[..6], "دمبل"))).Content.ReadFromJsonAsync<BilingualReferenceResponse>();
        var category = await (await admin.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Profile foods " + Guid.NewGuid().ToString("N")[..6], null))).Content.ReadFromJsonAsync<BilingualReferenceResponse>();
        var oats = await (await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Oats " + Guid.NewGuid().ToString("N")[..6], null, category!.Id, FoodBaseUnit.Gram, 380, 13, 60, 7, null))).Content.ReadFromJsonAsync<FoodResponse>();
        var coach = await factory.CreateCoachClientAsync();
        var otherCoach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);

        var empty = await coach.GetFromJsonAsync<ClientProfileResponse>($"/api/v1/clients/{client.Id}/profile");
        Assert.False(empty!.IsSaved);

        var saved = await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile",
            Profile(new DateOnly(1995, 5, 20), [equipment!.Id], [oats!.Id]));
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var profile = await saved.Content.ReadFromJsonAsync<ClientProfileResponse>();
        Assert.True(profile!.IsSaved);
        Assert.NotNull(profile.Age);
        Assert.Equal(equipment.Id, Assert.Single(profile.Equipment).Id);
        Assert.Equal(oats.NameEn, Assert.Single(profile.PreferredFoods).NameEn);
        Assert.Equal(ActivityLevel.VeryActive, profile.ActivityLevel);

        var tooYoung = await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile",
            Profile(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3), [], []));
        Assert.Equal(HttpStatusCode.BadRequest, tooYoung.StatusCode);
        Assert.Contains("dateOfBirth", await tooYoung.Content.ReadAsStringAsync());
        var unknownFood = await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile",
            Profile(new DateOnly(1995, 5, 20), [], [Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.BadRequest, unknownFood.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await otherCoach.GetAsync($"/api/v1/clients/{client.Id}/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherCoach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile", Profile(null, [], []))).StatusCode);
    }

    [Fact]
    public async Task Clients_can_be_filtered_by_status_including_manual_overrides()
    {
        var coach = await factory.CreateCoachClientAsync();
        var flagged = await CreateClientAsync(coach);
        var regular = await CreateClientAsync(coach);

        var set = await coach.PutAsJsonAsync($"/api/v1/clients/{flagged.Id}/status",
            new ClientStatusInput(ClientStatus.NeedsReview, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)));
        Assert.Equal(ClientStatus.NeedsReview, (await set.Content.ReadFromJsonAsync<ClientResponse>())!.Status);

        var needsReview = await coach.GetFromJsonAsync<PagedResult<ClientResponse>>("/api/v1/clients?status=NeedsReview&pageSize=100");
        var onTrack = await coach.GetFromJsonAsync<PagedResult<ClientResponse>>("/api/v1/clients?status=OnTrack&pageSize=100");
        Assert.Contains(needsReview!.Items, item => item.Id == flagged.Id);
        Assert.DoesNotContain(needsReview.Items, item => item.Id == regular.Id);
        Assert.Contains(onTrack!.Items, item => item.Id == regular.Id);
        Assert.DoesNotContain(onTrack.Items, item => item.Id == flagged.Id);

        var cleared = await coach.DeleteAsync($"/api/v1/clients/{flagged.Id}/status");
        Assert.Equal(ClientStatus.OnTrack, (await cleared.Content.ReadFromJsonAsync<ClientResponse>())!.Status);
    }

    [Fact]
    public async Task An_anonymous_password_reset_request_queues_account_mail()
    {
        // Regression: the anonymous auth surface used to be refused by the tenant save guard
        // when it queued the (platform) reset email.
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);
        using var anonymous = factory.CreateClient();

        var request = await anonymous.PostAsJsonAsync("/api/v1/auth/password-reset/request", new PasswordResetRequest(account.Email));

        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        using var scope = factory.Services.CreateScope();
        var mail = await scope.ServiceProvider.GetRequiredService<CoachHubDbContext>().Set<Notification>()
            .SingleAsync(item => item.Recipient == account.Email);
        Assert.True(mail.IsAccountMessage);
        Assert.Null(mail.CoachId);
    }

    private static ClientProfileInput Profile(DateOnly? dateOfBirth, Guid[] equipmentIds, Guid[] preferredFoodIds) => new(
        dateOfBirth, Sex.Male, 180m, 85m, 20m, FitnessGoal.BuildMuscle, ActivityLevel.VeryActive, 5, 9000,
        TrainingLocation.Gym, "Heavy compound lifts", equipmentIds, "High protein", preferredFoodIds, null, [], "Mushrooms",
        ["Halal"], null, 4, new TimeOnly(23, 30), new TimeOnly(7, 0), "Shift work");

    private async Task<(string Email, string Token)> InvitationAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        var body = await db.Set<Notification>().Where(item => item.Recipient == email && item.Channel == NotificationChannel.Email)
            .Select(item => item.Body).SingleAsync();
        var match = InvitationLink().Match(body);
        Assert.True(match.Success, body);
        return (Uri.UnescapeDataString(match.Groups["email"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    private static async Task<ClientResponse> CreateClientAsync(HttpClient coach)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Onboarded " + Guid.NewGuid().ToString("N")[..6], null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    [GeneratedRegex(@"email=(?<email>[^&\s]+)&token=(?<token>[^&\s]+)&invite=1")]
    private static partial Regex InvitationLink();
}
