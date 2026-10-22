using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Chat;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Progress;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Settings;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.IntegrationTests.Clients;

/// <summary>
/// P16 on SQL Server: subscription statuses (§47, §57) in code and SQL, suspension, payment status from invoices,
/// the Administrator's access rules and their enforcement on the client area. Data is never deleted.
/// </summary>
public sealed class SubscriptionAccessEndpointTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Statuses_suspension_and_payment_status_agree_in_code_and_sql()
    {
        var s = await SetUpAsync();
        async Task<ClientResponse> ClientWithAsync(string name, DateOnly start)
        {
            var client = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput($"{name} {s.Tag}", null, null, null)));
            await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/clients/{client.Id}/subscriptions",
                new SubscriptionInput(s.PackageId, start, 1, 100, s.CurrencyId, null, 0)));
            return client;
        }
        var active = await ClientWithAsync("Active", Today.AddDays(-3));
        var pending = await ClientWithAsync("Pending", Today.AddDays(5));
        var expired = await ClientWithAsync("Expired", Today.AddMonths(-3));
        var suspended = await ClientWithAsync("Suspended", Today.AddDays(-3));
        var none = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput($"None {s.Tag}", null, null, null)));

        // Suspension needs a reason, happens once, and keeps the end date.
        var subscription = (await s.Coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{suspended.Id}"))!.Subscriptions.Single();
        var path = $"/api/v1/clients/{suspended.Id}/subscriptions/{subscription.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PostAsJsonAsync($"{path}/suspend", new SubscriptionSuspendInput(" "))).StatusCode);
        var paused = await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsJsonAsync($"{path}/suspend", new SubscriptionSuspendInput("Payment late")));
        Assert.Equal((SubscriptionStatus.Suspended, "Payment late", subscription.EndDate, false), (paused.Status, paused.SuspensionReason, paused.EndDate, paused.IsActive));
        Assert.Equal(HttpStatusCode.Conflict, (await s.Coach.PostAsJsonAsync($"{path}/suspend", new SubscriptionSuspendInput("Again"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.PostAsJsonAsync($"{path}/resume", new { })).StatusCode);

        var expected = new Dictionary<Guid, SubscriptionStatus>
        {
            [active.Id] = SubscriptionStatus.Active, [pending.Id] = SubscriptionStatus.Pending, [expired.Id] = SubscriptionStatus.Expired,
            [suspended.Id] = SubscriptionStatus.Suspended, [none.Id] = SubscriptionStatus.Inactive,
        };
        foreach (var (id, status) in expected)
        {
            Assert.Equal(status, (await s.Coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{id}"))!.Client.SubscriptionStatus);
            var listed = (await s.Coach.GetFromJsonAsync<PagedResult<ClientResponse>>($"/api/v1/clients?subscriptionStatus={status}&searchTerm={s.Tag}&pageSize=50"))!;
            Assert.Equal([id], listed.Items.Select(x => x.Id));
        }
        // Only an ended subscription makes the client "subscription expired" (§39); suspended and pending do not.
        var expiredStatus = (await s.Coach.GetFromJsonAsync<PagedResult<ClientResponse>>($"/api/v1/clients?status=SubscriptionExpired&searchTerm={s.Tag}&pageSize=50"))!;
        Assert.Equal([expired.Id], expiredStatus.Items.Select(x => x.Id));
        Assert.Equal(ClientStatus.OnTrack, (await s.Coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{suspended.Id}"))!.Client.Status);

        var resumed = await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsync($"{path}/resume", null));
        Assert.Equal(SubscriptionStatus.Active, resumed.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Coach.PostAsync($"{path}/resume", null)).StatusCode);

        // Payment status follows the subscription's invoices.
        Assert.Equal(SubscriptionPaymentStatus.NoInvoice, resumed.PaymentStatus);
        var invoice = await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(suspended.Id, subscription.Id, s.CurrencyId, 100, null)));
        Assert.Equal(SubscriptionPaymentStatus.Unpaid, await PaymentAsync(s, suspended.Id));
        await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments", new PaymentInput(null, 40, null)));
        Assert.Equal(SubscriptionPaymentStatus.PartiallyPaid, await PaymentAsync(s, suspended.Id));
        await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments", new PaymentInput(null, 60, null)));
        Assert.Equal(SubscriptionPaymentStatus.Paid, await PaymentAsync(s, suspended.Id));
    }

    [Fact]
    public async Task Access_rules_restrict_the_client_area_without_deleting_data()
    {
        var s = await SetUpAsync();
        using var admin = factory.CreateAdminClient();
        var client = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput($"Member {s.Tag}", null, null, null)));
        var portal = factory.CreateClientAreaClient(s.CoachId, client.Id);
        var weight = $"/api/v1/me/progress/weight/{Today.AddDays(-1):yyyy-MM-dd}";
        var body = $"/api/v1/me/progress/body?from={Today.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}";

        // No subscription: full access by default; the client logs a weight.
        var access = (await portal.GetFromJsonAsync<MyAccessResponse>("/api/v1/me/access"))!;
        Assert.Equal(SubscriptionStatus.Inactive, access.SubscriptionStatus);
        Assert.All(access.Features, x => Assert.Equal(FeatureAccess.Full, x.Access));
        (await portal.PutAsJsonAsync(weight, new WeightInput(80.5m, null))).EnsureSuccessStatusCode();

        // An expired subscription: read-only by default. History stays visible; writing is refused with a reason.
        await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/clients/{client.Id}/subscriptions",
            new SubscriptionInput(s.PackageId, Today.AddMonths(-3), 1, 100, s.CurrencyId, null, 0)));
        access = (await portal.GetFromJsonAsync<MyAccessResponse>("/api/v1/me/access"))!;
        Assert.Equal((SubscriptionStatus.Expired, FeatureAccess.ReadOnly), (access.SubscriptionStatus, access.Features.Single(x => x.Feature == ClientFeature.Progress).Access));
        Assert.Equal(80.5m, (await portal.GetFromJsonAsync<BodyProgressResponse>(body))!.Weight.Summary.Current!.Value);
        var refused = await portal.PutAsJsonAsync(weight, new WeightInput(81m, null));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var problem = (await refused.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("Your subscription has ended, so progress is read-only: your history stays visible, but nothing new can be added.", problem.Detail);
        Assert.Equal("access.readOnly", problem.Extensions["code"]!.ToString());
        // P19: in Arabic when the client asks for it.
        using var arabic = new HttpRequestMessage(HttpMethod.Put, weight) { Content = JsonContent.Create(new WeightInput(81m, null)) };
        arabic.Headers.AcceptLanguage.ParseAdd("ar");
        var arabicProblem = (await (await portal.SendAsync(arabic)).Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(("غير متاح مع اشتراكك الحالي", "انتهى اشتراكك، لذلك أصبح قسم التقدم للقراءة فقط: يبقى سجلك ظاهرًا، لكن لا يمكن إضافة جديد."),
            (arabicProblem.Title, arabicProblem.Detail));
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync("/api/v1/me/chat")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PostAsJsonAsync("/api/v1/me/chat/messages", new ChatSendInput("Hello?"))).StatusCode);
        // The account and notifications are never restricted.
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync("/api/v1/inbox/unread-count")).StatusCode);

        // The Administrator's rules: coaches read them, only the Administrator changes them.
        var settings = (await s.Coach.GetFromJsonAsync<SubscriptionSettingsResponse>("/api/v1/settings/subscriptions"))!;
        Assert.Equal([1, 3, 6, 12], settings.DurationPresets);
        Assert.Equal(SubscriptionAccessDefaults.RestrictableStatuses.Count * Enum.GetValues<ClientFeature>().Length, settings.Rules.Count);
        Assert.All(settings.Rules, x => Assert.True(x.IsDefault));
        var input = new SubscriptionSettingsInput([1, 6, 12],
            [new(SubscriptionStatus.Expired, ClientFeature.Chat, FeatureAccess.Blocked), new(SubscriptionStatus.Expired, ClientFeature.Progress, FeatureAccess.Full)]);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Coach.PutAsJsonAsync("/api/v1/settings/subscriptions", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/settings/subscriptions",
            input with { Rules = [new(SubscriptionStatus.Active, ClientFeature.Chat, FeatureAccess.Blocked)] })).StatusCode);
        settings = await ReadAsync<SubscriptionSettingsResponse>(await admin.PutAsJsonAsync("/api/v1/settings/subscriptions", input));
        Assert.Equal([1, 6, 12], settings.DurationPresets);
        Assert.False(settings.Rules.Single(x => x.Status == SubscriptionStatus.Expired && x.Feature == ClientFeature.Chat).IsDefault);

        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/me/chat")).StatusCode);
        (await portal.PutAsJsonAsync(weight, new WeightInput(81m, null))).EnsureSuccessStatusCode();

        // A new subscription restores everything, with all the data still there.
        await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/clients/{client.Id}/subscriptions",
            new SubscriptionInput(s.PackageId, Today, 1, 100, s.CurrencyId, null, 0)));
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync("/api/v1/me/chat")).StatusCode);
        Assert.Equal(81m, (await portal.GetFromJsonAsync<BodyProgressResponse>(body))!.Weight.Summary.Current!.Value);

        // Restore the defaults for the other tests of this database.
        await ReadAsync<SubscriptionSettingsResponse>(await admin.PutAsJsonAsync("/api/v1/settings/subscriptions", new SubscriptionSettingsInput([1, 3, 6, 12], [])));
    }

    private static async Task<SubscriptionPaymentStatus> PaymentAsync(Setup s, Guid clientId) =>
        (await s.Coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{clientId}"))!.Subscriptions.Single().PaymentStatus;

    private sealed record Setup(Guid CoachId, HttpClient Coach, HttpClient OtherCoach, Guid PackageId, Guid CurrencyId, string Tag);

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        using var admin = factory.CreateAdminClient();
        var package = await ReadAsync<PackageResponse>(await coach.PostAsJsonAsync("/api/v1/reference-data/packages", new PackageInput("Package " + tag, null, null, true)));
        var currency = await ReadAsync<CurrencyResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("S" + tag[..4].ToUpperInvariant(), "Subscription currency", "$", true)));
        return new Setup(coachId, coach, factory.CreateCoachClient(await factory.CreateCoachAsync()), package.Id, currency.Id, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
