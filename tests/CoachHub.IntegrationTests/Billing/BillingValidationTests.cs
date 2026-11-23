using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Clients;
using CoachHub.Application.ReferenceData;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.IntegrationTests.Billing;

/// <summary>P23: billing rules answer 400 (bad input) or 409 (the invoice's state), never 500.</summary>
public sealed class BillingValidationTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Billing_rules_answer_400_or_409_in_the_request_language()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        using var admin = factory.CreateAdminClient();
        var currency = await ReadAsync<CurrencyResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("B" + tag[..4].ToUpperInvariant(), "Billing currency", "$", true)));
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput($"Payer {tag}", null, null, null)));

        // A due date before the issue date.
        var past = await coach.PostAsJsonAsync("/api/v1/billing/invoices", new InvoiceInput(client.Id, null, currency.Id, 100, DateTimeOffset.UtcNow.AddDays(-3)));
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains("Due date cannot precede issue date.", await past.Content.ReadAsStringAsync());
        using var arabic = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/invoices")
        {
            Content = JsonContent.Create(new InvoiceInput(client.Id, null, currency.Id, 100, DateTimeOffset.UtcNow.AddDays(-3)))
        };
        arabic.Headers.AcceptLanguage.ParseAdd("ar");
        Assert.Contains("لا يمكن أن يسبق تاريخ الاستحقاق تاريخ الإصدار.", await (await coach.SendAsync(arabic)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(client.Id, null, currency.Id, 0, null))).StatusCode);

        // Paying more than is due is a bad amount; voiding a paid invoice or paying a voided one is a conflict.
        var invoice = await ReadAsync<InvoiceResponse>(await coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(client.Id, null, currency.Id, 100, DateTimeOffset.UtcNow.AddDays(7))));
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments",
            new PaymentInput(null, 150, null))).StatusCode);
        await ReadAsync<InvoiceResponse>(await coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments", new PaymentInput(null, 40, null)));
        var voidPaid = await coach.PostAsync($"/api/v1/billing/invoices/{invoice.Id}/void", null);
        Assert.Equal(HttpStatusCode.Conflict, voidPaid.StatusCode);
        Assert.Equal("A paid invoice cannot be voided.", (await voidPaid.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);

        var unpaid = await ReadAsync<InvoiceResponse>(await coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(client.Id, null, currency.Id, 50, null)));
        Assert.True((await coach.PostAsync($"/api/v1/billing/invoices/{unpaid.Id}/void", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await coach.PostAsJsonAsync($"/api/v1/billing/invoices/{unpaid.Id}/payments",
            new PaymentInput(null, 10, null))).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
