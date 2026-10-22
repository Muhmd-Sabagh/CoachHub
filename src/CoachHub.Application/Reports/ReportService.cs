using System.Text;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Pdf;
using CoachHub.Application.Progress;
using CoachHub.Application.Settings;

namespace CoachHub.Application.Reports;

/// <summary>
/// Weekly and monthly client reports (§44), on demand for the coach (any of their clients) and the client (their
/// own). Built from <see cref="ProgressDashboardService"/>, so the figures match the dashboard; exported to PDF with the
/// coach's brand. Tenant filters decide who sees which client.
/// </summary>
public sealed class ReportService(
    ProgressDashboardService dashboard,
    ICheckInRepository checkIns,
    IPlanPdfClientRepository clients,
    BrandingService branding,
    IReportPdfRenderer renderer,
    TimeProvider timeProvider)
{
    public async Task<ClientReport> GetAsync(Guid clientId, ReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Enum.IsDefined(query.Period)) throw new ValidationException(new Dictionary<string, string[]> { ["period"] = ["Choose a week or a month."] });
        var client = await clients.FindAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var (from, to) = ReportPeriods.Range(query.Period, query.Date ?? today);
        var progress = await dashboard.DashboardAsync(clientId, from, to, cancellationToken);
        var periodCheckIns = (await checkIns.ListForClientAsync(clientId, cancellationToken))
            .Where(x => x.DueOn >= from && x.DueOn <= to)
            .OrderBy(x => x.DueOn)
            .Select(x => new ReportCheckIn(x.DueOn, x.Status, x.IsLate, x.SubmittedAt, x.Replies.Count))
            .ToArray();
        var brand = await branding.EffectiveAsync(await CoachOfAsync(clientId, cancellationToken), cancellationToken);
        return new ClientReport(clientId, client.Name, query.Period, from, to, timeProvider.GetUtcNow(),
            brand.BrandName, brand.PrimaryColor, progress, periodCheckIns);
    }

    public async Task<GeneratedPlanPdf> PdfAsync(Guid clientId, ReportQuery query, CancellationToken cancellationToken)
    {
        var report = await GetAsync(clientId, query, cancellationToken);
        var name = $"{Slug(report.ClientName)}-{(report.Period == ReportPeriod.Week ? "weekly" : "monthly")}-report-{report.From:yyyy-MM-dd}" +
            (query.Language == PlanPdfLanguage.Arabic ? "-ar" : string.Empty) + ".pdf";
        return new GeneratedPlanPdf(renderer.Render(report, query.Language), name);
    }

    private async Task<Guid?> CoachOfAsync(Guid clientId, CancellationToken cancellationToken) =>
        (await checkIns.FindClientAsync(clientId, cancellationToken))?.CoachId;

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        var slug = builder.ToString().Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Length == 0 ? "client" : slug;
    }
}
