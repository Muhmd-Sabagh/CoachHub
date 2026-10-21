using CoachHub.Application.Dashboards;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Dashboards;

/// <summary>
/// The rows revenue is made of (D6): payments settled in the period, in their invoice's currency, and refunds
/// recorded in the period. Commercial records keep the selling coach (D7), so revenue is that coach's. The tenant
/// filters keep a coach to their own rows.
/// </summary>
internal static class RevenueQuery
{
    public static async Task<IReadOnlyList<RevenueRow>> RowsAsync(CoachHubDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var payments = await (
            from payment in db.Set<Payment>().AsNoTracking()
            where payment.RecordedAt >= start && payment.RecordedAt < end &&
                payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed
            join invoice in db.Set<Invoice>().AsNoTracking() on payment.InvoiceId equals invoice.Id
            join currency in db.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id
            join coach in db.Set<Coach>().AsNoTracking() on payment.CoachId equals coach.Id
            join subscriptionValue in db.Set<Subscription>().AsNoTracking() on invoice.SubscriptionId equals subscriptionValue.Id into subscriptions
            from subscription in subscriptions.DefaultIfEmpty()
            join packageValue in db.Set<Package>().AsNoTracking() on subscription.PackageId equals packageValue.Id into packages
            from package in packages.DefaultIfEmpty()
            join accountValue in db.Set<PaymentAccount>().AsNoTracking() on payment.PaymentAccountId equals accountValue.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new RevenueRow(payment.CoachId, coach.DisplayName, currency.Id, currency.Code,
                package == null ? null : (Guid?)package.Id, package == null ? "No package" : package.NameEn,
                payment.PaymentAccountId, account == null ? "No payment account" : account.Name, payment.Amount, false))
            .ToArrayAsync(cancellationToken);

        var refunds = await (
            from refund in db.Set<Refund>().AsNoTracking()
            where refund.RecordedAt >= start && refund.RecordedAt < end
            join payment in db.Set<Payment>().AsNoTracking() on refund.PaymentId equals payment.Id
            join invoice in db.Set<Invoice>().AsNoTracking() on payment.InvoiceId equals invoice.Id
            join currency in db.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id
            join coach in db.Set<Coach>().AsNoTracking() on refund.CoachId equals coach.Id
            join subscriptionValue in db.Set<Subscription>().AsNoTracking() on invoice.SubscriptionId equals subscriptionValue.Id into subscriptions
            from subscription in subscriptions.DefaultIfEmpty()
            join packageValue in db.Set<Package>().AsNoTracking() on subscription.PackageId equals packageValue.Id into packages
            from package in packages.DefaultIfEmpty()
            join accountValue in db.Set<PaymentAccount>().AsNoTracking() on payment.PaymentAccountId equals accountValue.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new RevenueRow(refund.CoachId, coach.DisplayName, currency.Id, currency.Code,
                package == null ? null : (Guid?)package.Id, package == null ? "No package" : package.NameEn,
                payment.PaymentAccountId, account == null ? "No payment account" : account.Name, refund.Amount, true))
            .ToArrayAsync(cancellationToken);

        return [.. payments, .. refunds];
    }
}
