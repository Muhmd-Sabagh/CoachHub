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
    private readonly HttpClient _client;

    public ReportingEndpointTests(CoachHubApiFactory factory) =>
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Administrator_receives_operational_and_separated_commercial_metrics()
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
            $"/api/clients/{client.Id}/subscriptions",
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
            $"/api/clients/{client.Id}/subscriptions",
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
            $"/api/reporting/overview?from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.NotNull(report);
        Assert.Equal(today.AddDays(-1), report.From);
        Assert.Equal(today, report.To);
        Assert.Equal(1, report.Clients.NewInPeriod);
        Assert.Equal(1, report.Clients.ClientsWithActiveSubscription);
        Assert.Equal(0, report.Clients.ClientsWithOnlyExpiredSubscriptions);
        Assert.Equal(0, report.Clients.ClientsWithoutSubscriptions);
        Assert.Equal(2, report.ByCurrency.Count);
        var currencyTotal = Assert.Single(report.ByCurrency, item => item.Id == currency.Id);
        Assert.Equal(1, currencyTotal.SubscriptionTransactions);
        Assert.Equal(0, currencyTotal.RenewalTransactions);
        Assert.Equal(1250, currencyTotal.Amount);
        Assert.Equal(currency.Code, currencyTotal.CurrencyCode);
        Assert.Equal(2, report.ByPackage.Count);
        Assert.All(report.ByPackage, item => Assert.Equal(package.Id, item.Id));
        Assert.Contains(report.ByPackage, item => item.CurrencyCode == currency.Code && item.Amount == 1250);
        Assert.Contains(report.ByPackage, item => item.CurrencyCode == secondCurrency.Code && item.Amount == 500);
        Assert.Equal(2, report.ByPaymentAccount.Count);
        Assert.All(report.ByPaymentAccount, item => Assert.Equal(account.Id, item.Id));
        Assert.Contains(report.ByPaymentAccount, item => item.CurrencyCode == currency.Code);
        Assert.Contains(report.ByPaymentAccount, item => item.CurrencyCode == secondCurrency.Code);
        var expiring = Assert.Single(report.ExpiringSubscriptions);
        Assert.Equal(client.Id, expiring.ClientId);
        Assert.InRange(expiring.DaysRemaining, 27, 30);
    }

    [Fact]
    public async Task Reporting_endpoint_rejects_anonymous_and_invalid_periods()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/reporting/overview")).StatusCode);

        await AuthenticateAsync();
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await _client.GetAsync(
                "/api/reporting/overview?from=2026-08-14&to=2026-08-13")).StatusCode);
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<ClientResponse> CreateClientAsync(string name, DateOnly joinDate)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/clients",
            new ClientCreateInput(name, null, null, joinDate));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private async Task<PackageResponse> CreatePackageAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reference-data/packages",
            new PackageInput(name, null, null, true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PackageResponse>())!;
    }

    private async Task<CurrencyResponse> CreateCurrencyAsync(string code)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reference-data/currencies",
            new CurrencyInput(code, "Reporting currency", "$", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrencyResponse>())!;
    }

    private async Task<PaymentAccountResponse> CreatePaymentAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reference-data/payment-accounts",
            new PaymentAccountInput(name, "Reporting", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentAccountResponse>())!;
    }
}