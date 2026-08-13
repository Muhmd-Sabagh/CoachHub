namespace CoachHub.Application.Reporting;

public sealed record ReportingQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    public ReportingQuery Normalize(DateOnly today)
    {
        var to = To ?? today;
        return this with { From = From ?? to.AddDays(-29), To = to };
    }
}

public sealed record ClientMetrics(
    int Total,
    int ActiveRecords,
    int ClientsWithActiveSubscription,
    int ClientsWithOnlyExpiredSubscriptions,
    int ClientsWithoutSubscriptions,
    int NewInPeriod);

public sealed record WorkflowMetrics(int DietReviewRequired, int WorkoutReviewRequired);

public sealed record AssessmentMetrics(int InitialSubmissions, int UpdateSubmissions);

public sealed record PlanMetrics(int AssignedDietPlans, int AssignedWorkoutPlans);

public sealed record CommercialBreakdown(
    string Key,
    Guid? Id,
    string Label,
    string CurrencyCode,
    int SubscriptionTransactions,
    int RenewalTransactions,
    decimal Amount);

public sealed record ExpiringSubscription(
    Guid SubscriptionId,
    Guid ClientId,
    string ClientName,
    string PackageName,
    string CurrencyCode,
    DateOnly EndDate,
    int DaysRemaining);

public sealed record OperationalReport(
    DateOnly From,
    DateOnly To,
    DateOnly AsOf,
    ClientMetrics Clients,
    WorkflowMetrics Workflow,
    AssessmentMetrics Assessments,
    PlanMetrics Plans,
    IReadOnlyList<CommercialBreakdown> ByCurrency,
    IReadOnlyList<CommercialBreakdown> ByPackage,
    IReadOnlyList<CommercialBreakdown> ByPaymentAccount,
    IReadOnlyList<ExpiringSubscription> ExpiringSubscriptions);

public interface IReportingRepository
{
    Task<OperationalReport> GetAsync(
        DateOnly from,
        DateOnly to,
        DateOnly today,
        CancellationToken cancellationToken);
}