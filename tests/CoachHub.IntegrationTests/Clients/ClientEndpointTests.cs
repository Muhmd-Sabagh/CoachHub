using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.API.Clients;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Clients;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Clients;

public sealed class ClientEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;

    public ClientEndpointTests(CoachHubApiFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Administrator_can_manage_client_subscription_and_derived_status()
    {
        await AuthenticateAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var package = await CreatePackageAsync("Package " + suffix);
        var currency = await CreateCurrencyAsync("C" + suffix[..5].ToUpperInvariant());
        var payment = await CreatePaymentAsync("Account " + suffix);

        var create = await _client.PostAsJsonAsync(
            "/api/clients",
            new ClientCreateInput(
                "  Phase Eight Client " + suffix,
                "+201000000000",
                "CLIENT." + suffix + "@EXAMPLE.COM",
                new DateOnly(2026, 8, 1)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var client = await create.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(client);
        Assert.Equal(8, client.ClientCode.Length);
        Assert.Equal(10, client.FormCode.Length);
        Assert.Equal(SubscriptionStatus.Inactive, client.SubscriptionStatus);
        Assert.Equal(client.Email, client.Email!.ToLowerInvariant());

        var originalFormCode = client.FormCode;
        var rotate = await _client.PostAsync(
            $"/api/clients/{client.Id}/form-code/regenerate",
            null);
        rotate.EnsureSuccessStatusCode();
        var rotated = await rotate.Content.ReadFromJsonAsync<FormCodeResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(originalFormCode, rotated.FormCode);

        var subscriptionCreate = await _client.PostAsJsonAsync(
            $"/api/clients/{client.Id}/subscriptions",
            new SubscriptionInput(
                package.Id,
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                2,
                1200,
                currency.Id,
                payment.Id,
                0));
        Assert.Equal(HttpStatusCode.Created, subscriptionCreate.StatusCode);
        var subscription = await subscriptionCreate.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(subscription);
        Assert.True(subscription.IsActive);
        Assert.Equal(subscription.StartDate.AddMonths(2), subscription.EndDate);

        var detail = await _client.GetFromJsonAsync<ClientDetailResponse>($"/api/clients/{client.Id}");
        Assert.NotNull(detail);
        Assert.Equal(SubscriptionStatus.Active, detail.Client.SubscriptionStatus);
        Assert.Single(detail.Subscriptions);

        var activePage = await _client.GetFromJsonAsync<PagedResult<ClientResponse>>(
            $"/api/clients?searchTerm={client.ClientCode}&subscriptionStatus=Active&pageSize=10");
        Assert.NotNull(activePage);
        Assert.Single(activePage.Items);

        var update = await _client.PutAsJsonAsync(
            $"/api/clients/{client.Id}",
            new ClientUpdateInput(
                client.Name,
                client.Phone,
                client.Email,
                PlanWorkflowStatus.WaitingForPlan,
                PlanWorkflowStatus.ReviewRequired,
                true));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.Equal(PlanWorkflowStatus.ReviewRequired, updated!.WorkoutStatus);
        Assert.Equal(client.ClientCode, updated.ClientCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync($"/api/reference-data/packages/{package.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync($"/api/reference-data/currencies/{currency.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync($"/api/reference-data/payment-accounts/{payment.Id}")).StatusCode);

        var removeSubscription = await _client.DeleteAsync(
            $"/api/clients/{client.Id}/subscriptions/{subscription.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeSubscription.StatusCode);
        var afterDelete = await _client.GetFromJsonAsync<ClientDetailResponse>($"/api/clients/{client.Id}");
        Assert.Equal(SubscriptionStatus.Inactive, afterDelete!.Client.SubscriptionStatus);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/clients/{client.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_subscription_reference_and_ranges_are_validation_errors()
    {
        await AuthenticateAsync();
        var client = await CreateClientAsync("Validation " + Guid.NewGuid().ToString("N"));

        var response = await _client.PostAsJsonAsync(
            $"/api/clients/{client.Id}/subscriptions",
            new SubscriptionInput(
                Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow),
                0,
                0,
                Guid.NewGuid(),
                null,
                -1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Client_endpoints_reject_anonymous_access()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/clients")).StatusCode);
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<ClientResponse> CreateClientAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/clients",
            new ClientCreateInput(name, null, null, null));
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
            new CurrencyInput(code, "Test Currency", "$", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CurrencyResponse>())!;
    }

    private async Task<PaymentAccountResponse> CreatePaymentAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reference-data/payment-accounts",
            new PaymentAccountInput(name, "Test", true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentAccountResponse>())!;
    }
}