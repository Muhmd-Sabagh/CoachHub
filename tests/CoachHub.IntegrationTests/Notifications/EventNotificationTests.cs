using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Communications;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Notifications;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Notifications;

/// <summary>
/// P14 on SQL Server: the §42 events. Plan activation, new versions and in-place saves (owner decision 2026-10-22),
/// check-in reviews, the scheduled check-in and subscription events, in-app delivery once per occurrence, and the
/// account's email preferences feeding the outbox.
/// </summary>
public sealed class EventNotificationTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Plan_events_notify_the_client_once_with_email_by_preference()
    {
        var s = await SetUpAsync();

        // Email preferences: every event but messages by default; the client keeps only new programs.
        var preferences = (await s.Portal.GetFromJsonAsync<NotificationPreferencesResponse>("/api/v1/inbox/preferences"))!;
        Assert.Contains(NotificationEvent.NewDiet, preferences.EmailEvents);
        Assert.DoesNotContain(NotificationEvent.NewMessage, preferences.EmailEvents);
        preferences = await ReadAsync<NotificationPreferencesResponse>(await s.Portal.PutAsJsonAsync("/api/v1/inbox/preferences",
            new NotificationPreferencesInput([NotificationEvent.NewProgram])));
        Assert.Equal([NotificationEvent.NewProgram], preferences.EmailEvents);

        // A plan created Active for the client is one "new diet", although it is both activated and assigned.
        var plan = await ReadAsync<DietPlanResponse>(await s.Coach.PostAsJsonAsync("/api/v1/diet-plans", Plan("Cut " + s.Tag, s.ClientId)));
        var inbox = await InboxAsync(s.Portal);
        var created = Assert.Single(inbox);
        Assert.Equal((NotificationEvent.NewDiet, "/app/nutrition", (DateTimeOffset?)null), (created.Event, created.Link, created.ReadAt));
        Assert.Contains("Cut " + s.Tag, created.Body);

        // Saving the active version in place is "program updated", once an hour; a new version is a new program.
        var path = $"/api/v1/diet-plans/{plan.Id}";
        (await s.Coach.PutAsJsonAsync($"{path}/revisions/current", Plan("Cut " + s.Tag, s.ClientId) with { BaseVersionNumber = 1 })).EnsureSuccessStatusCode();
        (await s.Coach.PutAsJsonAsync($"{path}/revisions/current", Plan("Cut " + s.Tag, s.ClientId) with { BaseVersionNumber = 1 })).EnsureSuccessStatusCode();
        (await s.Coach.PostAsJsonAsync($"{path}/revisions", Plan("Cut " + s.Tag, s.ClientId) with { BaseVersionNumber = 1 })).EnsureSuccessStatusCode();
        inbox = await InboxAsync(s.Portal);
        Assert.Equal(1, inbox.Count(x => x.Event == NotificationEvent.ProgramUpdated));
        Assert.Equal("Version 2 of your diet plan is now active.", Assert.Single(inbox, x => x.Event == NotificationEvent.NewProgram).Body);

        // A draft notifies nobody until it becomes Active.
        var draft = await ReadAsync<DietPlanResponse>(await s.Coach.PostAsJsonAsync("/api/v1/diet-plans",
            Plan("Bulk " + s.Tag, s.ClientId) with { Status = PlanRevisionStatus.Draft }));
        Assert.Equal(inbox.Count, (await InboxAsync(s.Portal)).Count);
        (await s.Coach.PutAsJsonAsync($"/api/v1/diet-plans/{draft.Id}/revisions/current", Plan("Bulk " + s.Tag, s.ClientId) with { BaseVersionNumber = 1 }))
            .EnsureSuccessStatusCode();
        Assert.Equal(2, (await InboxAsync(s.Portal)).Count(x => x.Event == NotificationEvent.NewDiet));

        // Email copies follow the preference: the new program only.
        var outbox = (await s.Coach.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!.Where(x => x.Recipient == s.ClientEmail).ToArray();
        Assert.Equal(("New version of Cut " + s.Tag, NotificationChannel.Email), (Assert.Single(outbox).Subject, outbox[0].Channel));

        // Reading is per account: the coach cannot read the client's notification.
        Assert.Equal(HttpStatusCode.NotFound, (await s.Coach.PostAsync($"/api/v1/inbox/{created.Id}/read", null)).StatusCode);
        Assert.NotNull((await ReadAsync<InboxItem>(await s.Portal.PostAsync($"/api/v1/inbox/{created.Id}/read", null))).ReadAt);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Portal.PostAsync("/api/v1/inbox/read-all", null)).StatusCode);
        Assert.Equal(0, (await s.Portal.GetFromJsonAsync<UnreadCountResponse>("/api/v1/inbox/unread-count"))!.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Portal.PutAsJsonAsync("/api/v1/inbox/preferences",
            new { emailEvents = new[] { "Unknown" } })).StatusCode);
    }

    [Fact]
    public async Task Check_in_and_subscription_events_notify_on_schedule_and_review()
    {
        var s = await SetUpAsync();
        using var admin = factory.CreateAdminClient();

        // A weekly check-in due in two days is open, so it is required.
        var form = await ReadAsync<FormVersionResponse>(await s.Coach.PostAsJsonAsync("/api/v1/assessment-forms",
            new CreateFormInput("Weekly " + s.Tag, AssessmentFormType.CheckIn)));
        var question = await ReadAsync<QuestionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/assessment-forms/{form.DefinitionId}/questions",
            new QuestionInput(null, "Energy", QuestionType.Number, true, 0, [], null)));
        (await s.Coach.PostAsync($"/api/v1/assessment-forms/{form.DefinitionId}/publish", null)).EnsureSuccessStatusCode();
        (await s.Coach.PutAsJsonAsync($"/api/v1/clients/{s.ClientId}/check-in-schedule",
            new CheckInScheduleInput(form.DefinitionId, 1, Today.AddDays(2).DayOfWeek, Today))).EnsureSuccessStatusCode();

        // Subscriptions all ended two days ago.
        var package = await ReadAsync<PackageResponse>(await s.Coach.PostAsJsonAsync("/api/v1/reference-data/packages",
            new PackageInput("Package " + s.Tag, null, null, true)));
        var currency = await ReadAsync<CurrencyResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("N" + s.Tag[..4].ToUpperInvariant(), "Test Currency", "$", true)));
        (await s.Coach.PostAsJsonAsync($"/api/v1/clients/{s.ClientId}/subscriptions",
            new SubscriptionInput(package.Id, Today.AddMonths(-1).AddDays(-2), 1, 100, currency.Id, null, 0))).EnsureSuccessStatusCode();

        await ScanAsync();
        await ScanAsync();
        var client = await InboxAsync(s.Portal);
        var required = Assert.Single(client, x => x.Event == NotificationEvent.CheckInRequired);
        Assert.Equal(("Time for your check-in", "/app/forms"), (required.Title, required.Link));
        Assert.Single(client, x => x.Event == NotificationEvent.SubscriptionExpired);
        var coach = await InboxAsync(s.Coach);
        Assert.Equal($"/clients/{s.ClientId}", Assert.Single(coach, x => x.Event == NotificationEvent.SubscriptionExpired).Link);

        // The client checks in; the coach's approval notifies them.
        (await s.Portal.PostAsJsonAsync($"/api/v1/me/forms/{form.DefinitionId}/submissions", new MyFormSubmissionInput(
            [new AnswerInput(question.Id, JsonSerializer.SerializeToElement(7), null)], Today))).EnsureSuccessStatusCode();
        var checkIn = Assert.Single((await s.Coach.GetFromJsonAsync<PagedResult<CheckInSummary>>($"/api/v1/check-ins?clientId={s.ClientId}"))!.Items);
        (await s.Coach.PostAsJsonAsync($"/api/v1/check-ins/{checkIn.Id}/approve", new CheckInReviewInput("Great week"))).EnsureSuccessStatusCode();
        (await s.Coach.PostAsJsonAsync($"/api/v1/check-ins/{checkIn.Id}/replies", new CheckInReplyInput("Keep going"))).EnsureSuccessStatusCode();
        var reviewed = (await InboxAsync(s.Portal)).Where(x => x.Event == NotificationEvent.CheckInReviewed).Select(x => x.Title).ToArray();
        Assert.Equal(["New reply on your check-in", "Check-in approved"], reviewed);
    }

    private async Task ScanAsync()
    {
        // Background work: no request, so the scan runs unrestricted across coaches.
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventNotificationScanner>().ScanAsync(CancellationToken.None);
    }

    private static DietPlanInput Plan(string name, Guid clientId) =>
        new(name, null, clientId, [], [new DietPlanVariantInput(Guid.NewGuid(), "Day", null, 0, true, null, [new MealInput(Guid.NewGuid(), "Breakfast", null, 0, null, [])], [])]);

    private static async Task<IReadOnlyList<InboxItem>> InboxAsync(HttpClient http) =>
        (await http.GetFromJsonAsync<PagedResult<InboxItem>>("/api/v1/inbox?pageSize=50"))!.Items;

    private sealed record Setup(HttpClient Coach, Guid ClientId, HttpClient Portal, string ClientEmail, string Tag);

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);
        var client = await ReadAsync<ClientResponse>(await coach.Http.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Notified " + tag, null, null, null)));
        var portal = await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id);
        return new Setup(coach.Http, client.Id, portal.Http, portal.Email, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
