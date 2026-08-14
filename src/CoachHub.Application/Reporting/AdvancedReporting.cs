namespace CoachHub.Application.Reporting;

public sealed record SettlementMetric(string CurrencyCode, decimal Invoiced, decimal Settled, decimal Refunded, decimal Outstanding);
public sealed record AdvancedReport(DateOnly From, DateOnly To, decimal AssessmentAdherencePercent, decimal RenewalRetentionPercent, int ClientsWithProgressHistory, int DeliveredPlans, int NotificationsSent, decimal NotificationSuccessPercent, IReadOnlyList<SettlementMetric> Settlement);
public interface IAdvancedReportingRepository { Task<AdvancedReport> GetAdvancedAsync(DateOnly from, DateOnly to, DateOnly today, CancellationToken token); }
public sealed class AdvancedReportingService(IAdvancedReportingRepository repository, TimeProvider timeProvider)
{
    public Task<AdvancedReport> GetAsync(ReportingQuery query, CancellationToken token)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var normalized = query.Normalize(today); var from = normalized.From!.Value; var to = normalized.To!.Value;
        if (from > to || to.DayNumber - from.DayNumber > 365) throw new ArgumentException("Reporting period must be ordered and no longer than 366 days.");
        return repository.GetAdvancedAsync(from, to, today, token);
    }
}
