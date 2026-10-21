using CoachHub.Domain.Auditing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;

namespace CoachHub.Application.Dashboards;

/// <summary>A dashboard period, by default the last 30 days; at most 366 days.</summary>
public sealed record DashboardQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>
/// Revenue (D6) for one group and currency: payments settled in the period minus refunds recorded in the period.
/// A refund counts when it is recorded, even for an earlier payment, so a period's net can be negative.
/// </summary>
public sealed record RevenueLine(
    string Key,
    Guid? Id,
    string Label,
    string CurrencyCode,
    int Payments,
    int Refunds,
    decimal Settled,
    decimal Refunded,
    decimal Net);

/// <summary>A settled payment (positive) or a refund, with what it can be grouped by.</summary>
public sealed record RevenueRow(
    Guid CoachId,
    string CoachName,
    Guid CurrencyId,
    string CurrencyCode,
    Guid? PackageId,
    string PackageName,
    Guid? PaymentAccountId,
    string PaymentAccountName,
    decimal Amount,
    bool IsRefund);

public sealed record ClientCounts(int Total, int Active, int Inactive);

public sealed record StatusCount(ClientStatus Status, int Count);

/// <summary>Check-ins waiting for the coach, open now (due within the window), and overdue.</summary>
public sealed record CheckInCounts(int AwaitingReview, int Due, int Overdue);

/// <summary>A client's active plan whose program end date has passed with no other plan of its kind in force.</summary>
public sealed record ExpiredProgram(Guid ClientId, string ClientName, PlanKind Kind, Guid PlanId, string PlanName, DateOnly EndedOn);

/// <summary>A recent change from the audit trail (D2).</summary>
public sealed record ActivityItem(
    DateTimeOffset At,
    string Actor,
    AuditActorKind ActorKind,
    string EntityType,
    AuditOperation Operation,
    Guid? ClientId,
    string? ClientName);

/// <summary>The coach dashboard (§38) with revenue (D6). Status counts link to the filtered client list.</summary>
public sealed record CoachDashboard(
    DateOnly From,
    DateOnly To,
    DateOnly AsOf,
    ClientCounts Clients,
    IReadOnlyList<StatusCount> Statuses,
    CheckInCounts CheckIns,
    int ExpiredProgramCount,
    IReadOnlyList<ExpiredProgram> ExpiredPrograms,
    IReadOnlyList<ActivityItem> RecentActivity,
    IReadOnlyList<RevenueLine> Revenue);

public sealed record NewUsers(int Coaches, int Clients);

/// <summary>Clients whose subscriptions have all ended, and subscriptions that ended in the period.</summary>
public sealed record ExpiredSubscriptions(int Clients, int EndedInPeriod);

/// <summary>One day of user activity: accounts that signed in or changed something, and the changes made.</summary>
public sealed record DailyActivity(DateOnly Date, int ActiveUsers, int Changes);

/// <summary>What the platform was used for in the period.</summary>
public sealed record PlatformUsage(
    int WorkoutsLogged,
    int FoodEntries,
    int ProgressEntries,
    int CheckInsSent,
    int ChatMessages,
    int FilesShared,
    int PlansDelivered,
    int FormSubmissions);

/// <summary>The administrator dashboard (§48) with revenue per currency and per coach (D6, D7).</summary>
public sealed record AdminDashboard(
    DateOnly From,
    DateOnly To,
    DateOnly AsOf,
    int Clients,
    int Coaches,
    int ActiveCoaches,
    int ActiveUsers,
    NewUsers NewUsers,
    ExpiredSubscriptions ExpiredSubscriptions,
    IReadOnlyList<RevenueLine> RevenueByCurrency,
    IReadOnlyList<RevenueLine> RevenueByCoach,
    IReadOnlyList<DailyActivity> UserActivity,
    PlatformUsage Usage);

/// <summary>The coach figures, before revenue is summarised.</summary>
public sealed record CoachFigures(
    ClientCounts Clients,
    IReadOnlyList<StatusCount> Statuses,
    CheckInCounts CheckIns,
    IReadOnlyList<ExpiredProgram> ExpiredPrograms,
    IReadOnlyList<ActivityItem> RecentActivity);

/// <summary>The platform figures, before revenue is summarised.</summary>
public sealed record AdminFigures(
    int Clients,
    int Coaches,
    int ActiveCoaches,
    int ActiveUsers,
    NewUsers NewUsers,
    ExpiredSubscriptions ExpiredSubscriptions,
    IReadOnlyList<DailyActivity> UserActivity,
    PlatformUsage Usage);

public interface IDashboardRepository
{
    /// <summary>Payments settled and refunds recorded in the period, within the caller's tenant.</summary>
    Task<IReadOnlyList<RevenueRow>> RevenueRowsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<CoachFigures> CoachAsync(DateOnly from, DateOnly to, DateOnly today, int checkInOpensDaysBefore, CancellationToken cancellationToken);
    Task<AdminFigures> AdminAsync(DateOnly from, DateOnly to, DateOnly today, CancellationToken cancellationToken);
}
