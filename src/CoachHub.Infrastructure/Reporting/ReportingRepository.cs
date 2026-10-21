using CoachHub.Application.Dashboards;
using CoachHub.Application.Reporting;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Dashboards;
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

        // Revenue (D6): settled payments minus refunds recorded in the period, per currency.
        var revenue = await RevenueQuery.RowsAsync(dbContext, periodFrom, periodTo, cancellationToken);
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
            RevenueCalculator.ByCurrency(revenue),
            RevenueCalculator.ByPackage(revenue),
            RevenueCalculator.ByPaymentAccount(revenue),
            expiring);
    }
}
