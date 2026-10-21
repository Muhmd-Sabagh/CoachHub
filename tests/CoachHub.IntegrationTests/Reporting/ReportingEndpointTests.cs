using CoachHub.Application.Billing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Reporting;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Reporting;

public sealed class ReportingEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public ReportingEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _admin = factory.CreateAdminClient();
    }

    [Fact]
    public async Task Administrator_receives_operational_metrics_and_revenue_per_currency()
    {
        await AuthenticateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var package = await CreatePackageAsync("Reporting package " + suffix);
        var currency = await CreateCurrencyAsync("Q" + suffix[..5].ToUpperInvariant());
        var secondCurrency = await CreateCurrencyAsync("Z" + suffix[..5].ToUpperInvariant());
        var account = await CreatePaymentAsync("Reporting account " + suffix);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var client = await CreateClientAsync("Reporting client " + suffix, today);

        var subscriptionResponse = await _client.PostAsJsonAsync(
            $"/api/v1/clients/{client.Id}/subscriptions",
            new SubscriptionInput(
                package.Id,
                today.AddDays(-1),
                1,
                1250,
                currency.Id,
                account.Id,
                0));
        subscriptionResponse.EnsureSuccessStatusCode();
        var secondSubscription = await _client.PostAsJsonAsync(
            $"/api/v1/clients/{client.Id}/subscriptions",
            new SubscriptionInput(
                package.Id,
                today.AddDays(-1),
                2,
                500,
                secondCurrency.Id,
                account.Id,
                0));
        secondSubscription.EnsureSuccessStatusCode();

        var report = await _client.GetFromJsonAsync<OperationalReport>(
            $"/api/v1/reporting/overview?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.NotNull(report);
        Assert.Equal(today.AddDays(-1), report.From);
        Assert.Equal(today, report.To);
        Assert.Equal(1, report.Clients.NewInPeriod);
        Assert.Equal(1, report.Clients.ClientsWithActiveSubscription);
        Assert.Equal(0, report.Clients.ClientsWithOnlyExpiredSubscriptions);
        Assert.Equal(0, report.Clients.ClientsWithoutSubscriptions);
        // Subscriptions alone are not revenue (D6).
        Assert.Empty(report.ByCurrency);

        // Revenue: payments settled in the period minus refunds recorded in it, per currency.
        var invoice = await ReadAsync<InvoiceResponse>(await _client.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(client.Id, null, currency.Id, 1250, null)));
        invoice = await ReadAsync<InvoiceResponse>(await _client.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments",
            new PaymentInput(account.Id, 1000, "TX-1")));
        await ReadAsync<InvoiceResponse>(await _client.PostAsJsonAsync($"/api/v1/billing/invoices/payments/{invoice.Payments[0].Id}/refunds",
            new RefundInput(200, "Partial refund")));
        var second = await ReadAsync<InvoiceResponse>(await _client.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(client.Id, null, secondCurrency.Id, 500, null)));
        await ReadAsync<InvoiceResponse>(await _client.PostAsJsonAsync($"/api/v1/billing/invoices/{second.Id}/payments",
            new PaymentInput(null, 500, null)));
        report = await _client.GetFromJsonAsync<OperationalReport>(
            $"/api/v1/reporting/overview?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(2, report!.ByCurrency.Count);
        var main = Assert.Single(report.ByCurrency, item => item.Id == currency.Id);
        Assert.Equal((currency.Code, 1, 1, 1000m, 200m, 800m), (main.CurrencyCode, main.Payments, main.Refunds, main.Settled, main.Refunded, main.Net));
        Assert.Equal(500m, Assert.Single(report.ByCurrency, item => item.Id == secondCurrency.Id).Net);
        Assert.All(report.ByPackage, item => Assert.Equal("No package", item.Label));
        Assert.Equal(800m, Assert.Single(report.ByPaymentAccount, item => item.Id == account.Id).Net);
        Assert.Equal(500m, Assert.Single(report.ByPaymentAccount, item => item.Id == null).Net);
        var expiring = Assert.Single(report.ExpiringSubscriptions);
        Assert.Equal(client.Id, expiring.ClientId);
        Assert.InRange(expiring.DaysRemaining, 27, 30);
    }

    [Fact]
    public async Task Reporting_endpoint_rejects_anonymous_and_invalid_periods()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/v1/reporting/overview")).StatusCode);

        await AuthenticateAsync();
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await _client.GetAsync(
                "/api/v1/reporting/overview?from=2026-08-14&to=2026-08-13")).StatusCode);
    }

    private System.Net.Http.Headers.AuthenticationHeaderValue? _coachAuthorization;

    /// <summary>
    /// Signs in as this test's coach (created on first use): tenant data is created in that
    /// coach's workspace, isolated from every other test.
    /// </summary>
    private async Task AuthenticateAsync()
    {
        _coachAuthorization ??= (await _factory.CreateCoachClientAsync()).DefaultRequestHeaders.Authorization;
        _client.DefaultRequestHeaders.Authorization = _coachAuthorization;
    }

    private async Task<ClientResponse> CreateClientAsync(string name, DateOnly joinDate)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/clients",
            new ClientCreateInput(name, null, null, joinDate));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private async Task<PackageResponse> CreatePackageAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/reference-data/packages",
            new PackageInput(name, null, null, true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PackageResponse>())!;
    }

    private async Task<CurrencyResponse> CreateCurrencyAsync(string code)
    {
        var response = await _admin.PostAsJsonAsync(
            "/api/v1/reference-data/currencies",
            new CurrencyInput(code, "Reporting currency", "$", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrencyResponse>())!;
    }

    private async Task<PaymentAccountResponse> CreatePaymentAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/reference-data/payment-accounts",
            new PaymentAccountInput(name, "Reporting", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentAccountResponse>())!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
