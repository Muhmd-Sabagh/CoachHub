using CoachHub.Application.Reporting;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Reporting;

public sealed class ReportingRepository(CoachHubDbContext dbContext) : IReportingRepository
{
    public async Task<OperationalReport> GetAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var clients = dbContext.Set<Client>().AsNoTracking();
        var subscriptions = dbContext.Set<Subscription>().AsNoTracking();
        var submissions = dbContext.Set<FormSubmission>().AsNoTracking();
        var periodStart = new DateTimeOffset(periodFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(periodTo.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var expiryEnd = today.AddDays(30);

        var totalClients = await clients.CountAsync(cancellationToken);
        var activeRecords = await clients.CountAsync(client => client.IsActive, cancellationToken);
        var activeSubscriptionClients = await clients.CountAsync(client =>
            client.Subscriptions.Any(subscription =>
                subscription.StartDate <= today && subscription.EndDate > today), cancellationToken);
        var expiredSubscriptionClients = await clients.CountAsync(client =>
            client.Subscriptions.Any() && !client.Subscriptions.Any(subscription =>
                subscription.StartDate <= today && subscription.EndDate > today), cancellationToken);
        var withoutSubscriptions = await clients.CountAsync(
            client => !client.Subscriptions.Any(), cancellationToken);
        var newClients = await clients.CountAsync(
            client => client.JoinDate >= periodFrom && client.JoinDate <= periodTo, cancellationToken);
        var dietReview = await clients.CountAsync(
            client => client.DietStatus == PlanWorkflowStatus.ReviewRequired, cancellationToken);
        var workoutReview = await clients.CountAsync(
            client => client.WorkoutStatus == PlanWorkflowStatus.ReviewRequired, cancellationToken);

        var initialSubmissions = await submissions.CountAsync(submission =>
            submission.FormType == AssessmentFormType.InitialAssessment &&
            submission.SubmittedAt >= periodStart && submission.SubmittedAt <= periodEnd,
            cancellationToken);
        var updateSubmissions = await submissions.CountAsync(submission =>
            submission.FormType == AssessmentFormType.UpdateAssessment &&
            submission.SubmittedAt >= periodStart && submission.SubmittedAt <= periodEnd,
            cancellationToken);
        var assignedDietPlans = await dbContext.Set<DietPlan>().AsNoTracking()
            .CountAsync(plan => plan.ClientId != null, cancellationToken);
        var assignedWorkoutPlans = await dbContext.Set<WorkoutPlan>().AsNoTracking()
            .CountAsync(plan => plan.ClientId != null, cancellationToken);

        var originalRows = await (
            from subscription in subscriptions
            join package in dbContext.Set<Package>().AsNoTracking()
                on subscription.PackageId equals package.Id
            join currency in dbContext.Set<Currency>().AsNoTracking()
                on subscription.CurrencyId equals currency.Id
            join accountValue in dbContext.Set<PaymentAccount>().AsNoTracking()
                on subscription.PaymentAccountId equals accountValue.Id into accountJoin
            from account in accountJoin.DefaultIfEmpty()
            where subscription.StartDate >= periodFrom && subscription.StartDate <= periodTo
            select new CommercialRow(
                subscription.PackageId,
                package.NameEn,
                subscription.CurrencyId,
                currency.Code,
                subscription.PaymentAccountId,
                account == null ? "No payment account" : account.Name,
                subscription.Price,
                false)).ToArrayAsync(cancellationToken);

        var renewalRows = await (
            from renewal in dbContext.Set<SubscriptionRenewal>().AsNoTracking()
            join subscription in subscriptions on renewal.SubscriptionId equals subscription.Id
            join package in dbContext.Set<Package>().AsNoTracking()
                on subscription.PackageId equals package.Id
            join currency in dbContext.Set<Currency>().AsNoTracking()
                on renewal.CurrencyId equals currency.Id
            join accountValue in dbContext.Set<PaymentAccount>().AsNoTracking()
                on renewal.PaymentAccountId equals accountValue.Id into accountJoin
            from account in accountJoin.DefaultIfEmpty()
            where renewal.RecordedAt >= periodStart && renewal.RecordedAt <= periodEnd
            select new CommercialRow(
                subscription.PackageId,
                package.NameEn,
                renewal.CurrencyId,
                currency.Code,
                renewal.PaymentAccountId,
                account == null ? "No payment account" : account.Name,
                renewal.Price,
                true)).ToArrayAsync(cancellationToken);

        var commercialRows = originalRows.Concat(renewalRows).ToArray();
        var expiring = await (
            from subscription in subscriptions
            join client in clients on subscription.ClientId equals client.Id
            join package in dbContext.Set<Package>().AsNoTracking()
                on subscription.PackageId equals package.Id
            join currency in dbContext.Set<Currency>().AsNoTracking()
                on subscription.CurrencyId equals currency.Id
            where subscription.StartDate <= today &&
                subscription.EndDate > today && subscription.EndDate <= expiryEnd
            orderby subscription.EndDate, client.Name
            select new ExpiringSubscription(
                subscription.Id,
                client.Id,
                client.Name,
                package.NameEn,
                currency.Code,
                subscription.EndDate,
                subscription.EndDate.DayNumber - today.DayNumber))
            .Take(20)
            .ToArrayAsync(cancellationToken);

        return new OperationalReport(
            periodFrom,
            periodTo,
            today,
            new ClientMetrics(
                totalClients,
                activeRecords,
                activeSubscriptionClients,
                expiredSubscriptionClients,
                withoutSubscriptions,
                newClients),
            new WorkflowMetrics(dietReview, workoutReview),
            new AssessmentMetrics(initialSubmissions, updateSubmissions),
            new PlanMetrics(assignedDietPlans, assignedWorkoutPlans),
            Breakdown(commercialRows, row => (
                row.CurrencyId.ToString(),
                (Guid?)row.CurrencyId,
                row.CurrencyLabel,
                row.CurrencyLabel)),
            Breakdown(commercialRows, row => (
                $"{row.PackageId}:{row.CurrencyId}",
                (Guid?)row.PackageId,
                row.PackageLabel,
                row.CurrencyLabel)),
            Breakdown(commercialRows, row => (
                $"{row.PaymentAccountId?.ToString() ?? "none"}:{row.CurrencyId}",
                row.PaymentAccountId,
                row.PaymentAccountLabel,
                row.CurrencyLabel)),
            expiring);
    }

    private static IReadOnlyList<CommercialBreakdown> Breakdown(
        IEnumerable<CommercialRow> rows,
        Func<CommercialRow, (string Key, Guid? Id, string Label, string CurrencyCode)> keySelector) =>
        rows.GroupBy(keySelector)
            .Select(group => new CommercialBreakdown(
                group.Key.Key,
                group.Key.Id,
                group.Key.Label,
                group.Key.CurrencyCode,
                group.Count(row => !row.IsRenewal),
                group.Count(row => row.IsRenewal),
                group.Sum(row => row.Amount)))
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Label)
            .ToArray();

    private sealed record CommercialRow(
        Guid PackageId,
        string PackageLabel,
        Guid CurrencyId,
        string CurrencyLabel,
        Guid? PaymentAccountId,
        string PaymentAccountLabel,
        decimal Amount,
        bool IsRenewal);
}