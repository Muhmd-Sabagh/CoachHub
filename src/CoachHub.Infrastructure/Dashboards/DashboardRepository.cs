using CoachHub.Application.Dashboards;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Chat;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.ClientFiles;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.Progress;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.Infrastructure.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Dashboards;

public sealed class DashboardRepository(CoachHubDbContext dbContext) : IDashboardRepository
{
    private const int RecentActivityCount = 15;

    // Account and delivery bookkeeping (sign-ins, session refreshes, notifications) stays in the audit trail but would
    // crowd the coaching changes out of the dashboard's recent activity (P18).
    private static readonly string[] BookkeepingTypes = [nameof(UserSession), nameof(Notification)];

    public Task<IReadOnlyList<RevenueRow>> RevenueRowsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        RevenueQuery.RowsAsync(dbContext, from, to, cancellationToken);

    public async Task<CoachFigures> CoachAsync(DateOnly from, DateOnly to, DateOnly today, int checkInOpensDaysBefore, CancellationToken cancellationToken)
    {
        // Every query runs under the caller's tenant filters: a coach sees their own clients and records.
        var clients = dbContext.Set<Client>().AsNoTracking().SeekCoach(dbContext);
        var total = await clients.CountAsync(cancellationToken);
        var active = await clients.CountAsync(x => x.IsActive, cancellationToken);
        var statuses = new List<StatusCount>();
        foreach (var status in Enum.GetValues<ClientStatus>())
            statuses.Add(new StatusCount(status, await ClientRepository.WithStatus(clients, status, today).CountAsync(cancellationToken)));

        var openUntil = today.AddDays(checkInOpensDaysBefore);
        var checkIns = new CheckInCounts(
            await dbContext.Set<CheckIn>().AsNoTracking().SeekCoach(dbContext).CountAsync(x => x.Status == CheckInStatus.AwaitingReview, cancellationToken),
            await clients.CountAsync(x => x.IsActive && x.CheckInDueOn != null && x.CheckInDueOn >= today && x.CheckInDueOn <= openUntil, cancellationToken),
            await clients.CountAsync(x => x.IsActive && x.CheckInDueOn != null && x.CheckInDueOn < today, cancellationToken));

        var names = await clients.Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var expired = Expired(await CurrentPlansAsync(cancellationToken), names, today);

        var audit = await dbContext.Set<AuditEntry>().AsNoTracking().SeekOptionalCoach(dbContext).Where(x => !BookkeepingTypes.Contains(x.EntityType))
            .OrderByDescending(x => x.OccurredAt).Take(RecentActivityCount)
            .ToArrayAsync(cancellationToken);
        var auditClients = audit.Where(x => x.ClientId is { } id && !names.ContainsKey(id)).Select(x => x.ClientId!.Value).Distinct().ToArray();
        var otherNames = await clients.Where(x => auditClients.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var recent = audit.Select(x => new ActivityItem(x.OccurredAt, x.ActorDisplayName ?? x.ActorKind.ToString(), x.ActorKind, x.EntityType,
            x.Operation, x.ClientId,
            x.ClientId is { } id ? names.GetValueOrDefault(id) ?? otherNames.GetValueOrDefault(id) : null)).ToArray();

        return new CoachFigures(new ClientCounts(total, active, total - active), statuses, checkIns, expired, recent);
    }

    public async Task<AdminFigures> AdminAsync(DateOnly from, DateOnly to, DateOnly today, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var clients = dbContext.Set<Client>().AsNoTracking();
        var coaches = dbContext.Set<Coach>().AsNoTracking();

        // Activity: accounts that signed in (a session started or refreshed) or changed something (the audit trail).
        var sessionDays = await dbContext.Set<UserSession>().AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .Select(x => new { x.UserId, x.CreatedAt.Year, x.CreatedAt.Month, x.CreatedAt.Day }).Distinct().ToArrayAsync(cancellationToken);
        var refreshDays = await dbContext.Set<UserSession>().AsNoTracking()
            .Where(x => x.RotatedAt != null && x.RotatedAt >= start && x.RotatedAt < end)
            .Select(x => new { x.UserId, x.RotatedAt!.Value.Year, x.RotatedAt.Value.Month, x.RotatedAt.Value.Day }).Distinct().ToArrayAsync(cancellationToken);
        var auditDays = await dbContext.Set<AuditEntry>().AsNoTracking()
            .Where(x => x.OccurredAt >= start && x.OccurredAt < end)
            .GroupBy(x => new { x.ActorUserId, x.OccurredAt.Year, x.OccurredAt.Month, x.OccurredAt.Day })
            .Select(x => new { x.Key.ActorUserId, x.Key.Year, x.Key.Month, x.Key.Day, Changes = x.Count() }).ToArrayAsync(cancellationToken);
        var active = sessionDays.Select(x => (x.UserId, Day: new DateOnly(x.Year, x.Month, x.Day)))
            .Concat(refreshDays.Select(x => (x.UserId, Day: new DateOnly(x.Year, x.Month, x.Day))))
            .Concat(auditDays.Where(x => x.ActorUserId != null).Select(x => (UserId: x.ActorUserId!.Value, Day: new DateOnly(x.Year, x.Month, x.Day))))
            .Distinct().ToArray();
        var changes = auditDays.GroupBy(x => new DateOnly(x.Year, x.Month, x.Day)).ToDictionary(x => x.Key, x => x.Sum(y => y.Changes));
        var activeByDay = active.ToLookup(x => x.Day);
        var daily = new List<DailyActivity>();
        for (var day = from; day <= to && day <= today; day = day.AddDays(1))
            daily.Add(new DailyActivity(day, activeByDay[day].Count(), changes.GetValueOrDefault(day)));

        var progress =
            await dbContext.Set<WeightEntry>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken) +
            await dbContext.Set<BodyFatEntry>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken) +
            await dbContext.Set<BodyMeasurement>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken) +
            await dbContext.Set<SleepEntry>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken) +
            await dbContext.Set<StepsEntry>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken) +
            await dbContext.Set<WaterEntry>().CountAsync(x => x.Date >= from && x.Date <= to, cancellationToken);
        var usage = new PlatformUsage(
            await dbContext.Set<WorkoutSession>().CountAsync(x => x.CompletedAt >= start && x.CompletedAt < end, cancellationToken),
            await dbContext.Set<FoodLogEntry>().CountAsync(x => x.LoggedAt >= start && x.LoggedAt < end, cancellationToken),
            progress,
            await dbContext.Set<CheckInSubmission>().CountAsync(x => x.SubmittedAt >= start && x.SubmittedAt < end, cancellationToken),
            await dbContext.Set<ChatMessage>().CountAsync(x => x.SentAt >= start && x.SentAt < end, cancellationToken),
            await dbContext.Set<ClientFile>().CountAsync(x => x.SharedAt >= start && x.SharedAt < end, cancellationToken),
            await dbContext.Set<DeliveredPlan>().CountAsync(x => x.DeliveredAt >= start && x.DeliveredAt < end, cancellationToken),
            await dbContext.Set<FormSubmission>().CountAsync(x => x.SubmittedAt >= start && x.SubmittedAt < end, cancellationToken));

        return new AdminFigures(
            await clients.CountAsync(cancellationToken),
            await coaches.CountAsync(cancellationToken),
            await coaches.CountAsync(x => x.IsActive, cancellationToken),
            active.Select(x => x.UserId).Distinct().Count(),
            new NewUsers(
                await coaches.CountAsync(x => x.CreatedAt >= start && x.CreatedAt < end, cancellationToken),
                await clients.CountAsync(x => x.JoinDate >= from && x.JoinDate <= to, cancellationToken)),
            new ExpiredSubscriptions(
                await clients.CountAsync(x => x.IsActive && x.Subscriptions.Any() &&
                    !x.Subscriptions.Any(s => s.StartDate <= today && s.EndDate > today), cancellationToken),
                await dbContext.Set<Subscription>().CountAsync(x => x.EndDate >= from && x.EndDate <= to && x.EndDate <= today, cancellationToken)),
            daily,
            usage);
    }

    private sealed record CurrentPlan(PlanKind Kind, Guid PlanId, Guid ClientId, string Name, PlanRevisionStatus Status, DateOnly? StartDate, DateOnly? EndDate);

    /// <summary>The current (not archived) version of every client plan.</summary>
    private async Task<IReadOnlyList<CurrentPlan>> CurrentPlansAsync(CancellationToken cancellationToken)
    {
        var diets = await (
            from plan in dbContext.Set<DietPlan>().AsNoTracking()
            where plan.ClientId != null
            join revision in dbContext.Set<DietPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where revision.Status != PlanRevisionStatus.Archived
            select new CurrentPlan(PlanKind.Diet, plan.Id, plan.ClientId!.Value, plan.NameEn, revision.Status, revision.StartDate, revision.EndDate))
            .ToArrayAsync(cancellationToken);
        var workouts = await (
            from plan in dbContext.Set<WorkoutPlan>().AsNoTracking()
            where plan.ClientId != null
            join revision in dbContext.Set<WorkoutPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where revision.Status != PlanRevisionStatus.Archived
            select new CurrentPlan(PlanKind.Workout, plan.Id, plan.ClientId!.Value, plan.NameEn, revision.Status, revision.StartDate, revision.EndDate))
            .ToArrayAsync(cancellationToken);
        return [.. diets, .. workouts];
    }

    /// <summary>
    /// Expired programs (§38): for each active client and plan kind, the latest active plan whose end date has passed,
    /// when no active plan of that kind is in force today. Newest first.
    /// </summary>
    private static IReadOnlyList<ExpiredProgram> Expired(IReadOnlyList<CurrentPlan> plans, IReadOnlyDictionary<Guid, string> activeClients, DateOnly today) =>
        plans.Where(x => x.Status == PlanRevisionStatus.Active && activeClients.ContainsKey(x.ClientId))
            .GroupBy(x => (x.ClientId, x.Kind))
            .Where(group => !group.Any(x => (x.StartDate is null || x.StartDate <= today) && (x.EndDate is null || x.EndDate >= today)))
            .Select(group => group.Where(x => x.EndDate < today).MaxBy(x => x.EndDate))
            .Where(x => x is not null)
            .Select(x => new ExpiredProgram(x!.ClientId, activeClients[x.ClientId], x.Kind, x.PlanId, x.Name, x.EndDate!.Value))
            .OrderByDescending(x => x.EndedOn).ThenBy(x => x.ClientName)
            .ToArray();
}
