using CoachHub.Application.Reporting;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Reporting;

public sealed class AdvancedReportingRepository(CoachHubDbContext db) : IAdvancedReportingRepository
{
    public async Task<AdvancedReport> GetAdvancedAsync(DateOnly from, DateOnly to, DateOnly today, CancellationToken token)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var activeIds = db.Set<Client>().AsNoTracking().Where(c => c.Subscriptions.Any(s => s.StartDate <= today && s.EndDate > today)).Select(c => c.Id);
        var activeCount = await activeIds.CountAsync(token);
        var updateCount = await db.Set<FormSubmission>().AsNoTracking().Where(x => x.FormType == AssessmentFormType.UpdateAssessment && x.SubmittedAt >= start && x.SubmittedAt <= end && activeIds.Contains(x.ClientId)).Select(x => x.ClientId).Distinct().CountAsync(token);
        var progress = await db.Set<FormSubmission>().AsNoTracking().GroupBy(x => x.ClientId).Where(x => x.Count() >= 2).CountAsync(token);
        var expiring = await db.Set<Subscription>().AsNoTracking().CountAsync(x => x.EndDate >= from && x.EndDate <= to, token);
        var renewals = await db.Set<SubscriptionRenewal>().AsNoTracking().CountAsync(x => x.RecordedAt >= start && x.RecordedAt <= end, token);
        var delivered = await db.Set<DeliveredPlan>().AsNoTracking().CountAsync(x => x.DeliveredAt >= start && x.DeliveredAt <= end, token);
        var attempts = await db.Set<Notification>().AsNoTracking().CountAsync(x => x.ScheduledAt >= start && x.ScheduledAt <= end && x.AttemptCount > 0, token);
        var sent = await db.Set<Notification>().AsNoTracking().CountAsync(x => x.ScheduledAt >= start && x.ScheduledAt <= end && x.Status == NotificationStatus.Sent, token);
        var invoices = await (from invoice in db.Set<Invoice>().AsNoTracking() join currency in db.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id where invoice.IssuedAt >= start && invoice.IssuedAt <= end && invoice.Status != InvoiceStatus.Voided select new { currency.Code, invoice.Total, invoice.Paid }).ToArrayAsync(token);
        var refunds = await (from refund in db.Set<Refund>().AsNoTracking() join payment in db.Set<Payment>().AsNoTracking() on refund.PaymentId equals payment.Id join invoice in db.Set<Invoice>().AsNoTracking() on payment.InvoiceId equals invoice.Id join currency in db.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id where refund.RecordedAt >= start && refund.RecordedAt <= end select new { currency.Code, refund.Amount }).ToArrayAsync(token);
        var settlement = invoices.GroupBy(x => x.Code).Select(g => new SettlementMetric(g.Key, g.Sum(x => x.Total), g.Sum(x => x.Paid), refunds.Where(x => x.Code == g.Key).Sum(x => x.Amount), g.Sum(x => x.Total - x.Paid))).OrderBy(x => x.CurrencyCode).ToArray();
        return new(from, to, Percent(updateCount, activeCount), Percent(renewals, expiring), progress, delivered, sent, Percent(sent, attempts), settlement);
    }
    private static decimal Percent(int numerator, int denominator) => denominator == 0 ? 0 : Math.Round(numerator * 100m / denominator, 1);
}
