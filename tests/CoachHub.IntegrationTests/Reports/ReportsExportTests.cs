using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Clients;
using CoachHub.Application.Progress;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Reports;
using CoachHub.Domain.Auditing;
using CoachHub.Infrastructure.Persistence;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Reports;

/// <summary>
/// P17 on SQL Server: weekly and monthly reports (§44) as JSON and PDF for the coach and the client, CSV exports
/// (§51) for the coach and the Administrator, isolation (§62), CSV injection protection and the audit of exports (D2).
/// </summary>
public sealed class ReportsExportTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    [Fact]
    public async Task Coach_and_client_get_the_same_report_as_json_and_pdf()
    {
        var s = await SetUpAsync();
        (await s.Portal.PutAsJsonAsync($"/api/v1/me/progress/weight/{Monday:yyyy-MM-dd}", new WeightInput(82m, null))).EnsureSuccessStatusCode();
        (await s.Portal.PutAsJsonAsync($"/api/v1/me/progress/weight/{Today:yyyy-MM-dd}", new WeightInput(81m, null))).EnsureSuccessStatusCode();
        (await s.Portal.PutAsJsonAsync($"/api/v1/me/progress/steps/{Today:yyyy-MM-dd}", new StepsInput(9000, null))).EnsureSuccessStatusCode();

        var report = (await s.Coach.GetFromJsonAsync<ClientReport>($"{s.ReportPath}?period=Week&date={Today:yyyy-MM-dd}"))!;
        Assert.Equal((ReportPeriod.Week, Monday, Monday.AddDays(6), s.ClientName), (report.Period, report.From, report.To, report.ClientName));
        Assert.Equal(81m, report.Progress.Body.Weight.Current!.Value);
        Assert.Equal(9000, report.Progress.Lifestyle.AverageSteps);
        Assert.False(string.IsNullOrWhiteSpace(report.BrandName));
        var mine = (await s.Portal.GetFromJsonAsync<ClientReport>($"/api/v1/me/reports?period=Week&date={Today:yyyy-MM-dd}"))!;
        Assert.Equal((report.From, report.Progress.Body.Weight.Current!.Value), (mine.From, mine.Progress.Body.Weight.Current!.Value));
        var month = (await s.Coach.GetFromJsonAsync<ClientReport>($"{s.ReportPath}?period=Month&date={Today:yyyy-MM-dd}"))!;
        Assert.Equal((new DateOnly(Today.Year, Today.Month, 1), ReportPeriod.Month), (month.From, month.Period));

        foreach (var (http, path) in new[] { (s.Coach, $"{s.ReportPath}/pdf?period=Month"), (s.Portal, "/api/v1/me/reports/pdf?period=Week&language=Arabic") })
        {
            var pdf = await http.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
            Assert.EndsWith(".pdf", pdf.Content.Headers.ContentDisposition!.FileNameStar ?? pdf.Content.Headers.ContentDisposition.FileName);
        }

        // Isolation and roles.
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.GetAsync(s.ReportPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.GetAsync($"{s.ReportPath}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync(s.ReportPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Coach.GetAsync("/api/v1/me/reports")).StatusCode);
    }

    [Fact]
    public async Task Coaches_export_their_clients_and_administrators_the_platform()
    {
        var s = await SetUpAsync("=HYPERLINK(\"evil\")");
        using var admin = factory.CreateAdminClient();
        (await s.Portal.PutAsJsonAsync($"/api/v1/me/progress/weight/{Today:yyyy-MM-dd}", new WeightInput(80.4m, "after holiday"))).EnsureSuccessStatusCode();

        // The client list: the coach's own clients, with a dangerous name neutralised.
        var list = await CsvAsync(s.Coach, "/api/v1/exports/clients");
        Assert.StartsWith("Client ID,Name,Phone,Email,Join date,Active,Status,Subscription,Coach\r\n", list);
        Assert.Contains($"\"'=HYPERLINK(\"\"evil\"\") {s.Tag}\"", list);
        Assert.DoesNotContain(",=HYPERLINK", list);
        Assert.DoesNotContain(s.OtherClientName, list);

        // One client's data, one dataset or everything in a ZIP.
        var progress = await CsvAsync(s.Coach, $"/api/v1/exports/clients/{s.ClientId}/Progress");
        Assert.Contains($"{Today:yyyy-MM-dd},Weight,80.4,kg,Manual,after holiday", progress);
        Assert.Contains("Field,Value", await CsvAsync(s.Coach, $"/api/v1/exports/clients/{s.ClientId}/Client"));
        var zip = await s.Coach.GetAsync($"/api/v1/exports/clients/{s.ClientId}/All");
        Assert.Equal("application/zip", zip.Content.Headers.ContentType!.MediaType);
        using (var archive = new ZipArchive(await zip.Content.ReadAsStreamAsync()))
            Assert.Equal(["checkins", "client", "nutrition", "progress", "workouts"],
                archive.Entries.Select(x => x.Name.Split('-')[^1].Replace(".csv", "")).Order());
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.GetAsync($"/api/v1/exports/clients/{s.ClientId}/Progress")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.GetAsync($"/api/v1/exports/clients/{s.ClientId}/Everything")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync("/api/v1/exports/clients")).StatusCode);

        // Platform exports are the Administrator's; the revenue ledger nets to revenue (D6).
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Coach.GetAsync("/api/v1/exports/platform/Coaches")).StatusCode);
        Assert.Contains(s.CoachName, await CsvAsync(admin, "/api/v1/exports/platform/Coaches"));
        Assert.Contains(s.OtherClientName, await CsvAsync(admin, "/api/v1/exports/platform/Clients"));
        var currency = await ReadAsync<CurrencyResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("X" + s.Tag[..4].ToUpperInvariant(), "Export currency", "$", true)));
        var package = await ReadAsync<PackageResponse>(await s.Coach.PostAsJsonAsync("/api/v1/reference-data/packages", new PackageInput("Pack " + s.Tag, null, null, true)));
        var subscription = await ReadAsync<SubscriptionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/clients/{s.ClientId}/subscriptions",
            new SubscriptionInput(package.Id, Today, 3, 300, currency.Id, null, 0)));
        var invoice = await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(s.ClientId, subscription.Id, currency.Id, 300, null)));
        invoice = await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments", new PaymentInput(null, 300, "TX-9")));
        var subscriptions = await CsvAsync(admin, "/api/v1/exports/platform/Subscriptions");
        Assert.Contains($",Pack {s.Tag},{Today:yyyy-MM-dd},{Today.AddMonths(3):yyyy-MM-dd},3,Active,Paid,300,{currency.Code},0,", subscriptions);
        // Payment status comes from the invoice: fully paid before the refund.
        await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/payments/{invoice.Payments[0].Id}/refunds", new RefundInput(50, "Partial")));
        var ledger = (await CsvAsync(admin, $"/api/v1/exports/platform/Revenue?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))
            .Split("\r\n").Where(x => x.Contains(currency.Code)).ToArray();
        Assert.Equal(2, ledger.Length);
        Assert.Contains(ledger, x => x.Contains(",Payment,") && x.Contains($",{currency.Code},300,") && x.EndsWith(",TX-9"));
        Assert.Contains(ledger, x => x.Contains(",Refund,") && x.Contains($",{currency.Code},-50,"));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/exports/platform/Revenue?from={Today:yyyy-MM-dd}&to={Today.AddDays(-5):yyyy-MM-dd}")).StatusCode);

        // Every export is in the audit trail (D2).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        var audited = await db.Set<AuditEntry>().Where(x => x.EntityType == "DataExport").ToArrayAsync();
        Assert.Contains(audited, x => x.ClientId == s.ClientId && x.CoachId == s.CoachId && x.ChangesJson!.Contains("All"));
        Assert.Contains(audited, x => x.ChangesJson!.Contains("ClientList") && x.CoachId == s.CoachId);
        Assert.Contains(audited, x => x.ChangesJson!.Contains("PlatformRevenue"));
    }

    private static async Task<string> CsvAsync(HttpClient http, string path)
    {
        var response = await http.GetAsync(path);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    private sealed record Setup(Guid CoachId, string CoachName, HttpClient Coach, HttpClient OtherCoach, Guid ClientId, string ClientName,
        HttpClient Portal, string OtherClientName, string Tag)
    {
        public string ReportPath => $"/api/v1/clients/{ClientId}/reports";
    }

    private async Task<Setup> SetUpAsync(string? name = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachName = "Coach " + tag;
        var coachId = await factory.CreateCoachAsync(coachName);
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoachId = await factory.CreateCoachAsync();
        var otherCoach = factory.CreateCoachClient(otherCoachId);
        var clientName = (name ?? "Reporter") + " " + tag;
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput(clientName, null, null, null)));
        var other = await ReadAsync<ClientResponse>(await otherCoach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Other " + tag, null, null, null)));
        return new Setup(coachId, coachName, coach, otherCoach, client.Id, clientName, factory.CreateClientAreaClient(coachId, client.Id),
            other.Name, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
