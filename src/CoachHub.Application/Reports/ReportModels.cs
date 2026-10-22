using System.Text.Json.Serialization;
using CoachHub.Application.Pdf;
using CoachHub.Application.Progress;
using CoachHub.Domain.CheckIns;

namespace CoachHub.Application.Reports;

/// <summary>The report's period (§44): a Monday–Sunday week or a calendar month.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReportPeriod>))]
public enum ReportPeriod
{
    Week,
    Month
}

/// <summary>Which report: the period that contains <see cref="Date"/> (today by default).</summary>
public sealed record ReportQuery
{
    public ReportPeriod Period { get; init; } = ReportPeriod.Week;
    public DateOnly? Date { get; init; }
    public PlanPdfLanguage Language { get; init; } = PlanPdfLanguage.English;
}

/// <summary>A check-in of the period, as the report shows it; the coach's private notes are never included.</summary>
public sealed record ReportCheckIn(DateOnly DueOn, CheckInStatus Status, bool IsLate, DateTimeOffset SubmittedAt, int Replies);

/// <summary>
/// A client's weekly or monthly report (§44): weight, measurements, nutrition, training, steps, sleep, water,
/// adherence and check-ins for the period, from the same services as the progress dashboard (§37). Coach and client
/// see the same report.
/// </summary>
public sealed record ClientReport(
    Guid ClientId,
    string ClientName,
    ReportPeriod Period,
    DateOnly From,
    DateOnly To,
    DateTimeOffset GeneratedAt,
    string BrandName,
    string BrandColor,
    ProgressDashboardResponse Progress,
    IReadOnlyList<ReportCheckIn> CheckIns);

/// <summary>Renders a client report as a PDF with the coach's brand (QuestPDF in Infrastructure).</summary>
public interface IReportPdfRenderer
{
    byte[] Render(ClientReport report, PlanPdfLanguage language);
}

/// <summary>Report periods (§44), pure.</summary>
public static class ReportPeriods
{
    public static (DateOnly From, DateOnly To) Range(ReportPeriod period, DateOnly day)
    {
        if (period == ReportPeriod.Month)
        {
            var first = new DateOnly(day.Year, day.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1));
        }
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return (monday, monday.AddDays(6));
    }
}
