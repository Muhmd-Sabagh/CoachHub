using CoachHub.Application.Common.Events;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Notifications;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Notifications;

/// <summary>
/// Runs the handlers of saved domain events (P14). Handlers are resolved when an event is dispatched, so the
/// context that raises events never depends on them. A failing handler is logged and does not fail the save.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            foreach (var handler in services.GetServices(handlerType))
            {
                try
                {
                    await (Task)handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!
                        .Invoke(handler, [domainEvent, cancellationToken])!;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Handling {Event} failed.", domainEvent.GetType().Name);
                }
            }
        }
    }
}

/// <summary>Live delivery when no real-time transport is configured (background work, tools).</summary>
public sealed class NullRealtimePublisher : IRealtimePublisher
{
    public Task PublishAsync(IReadOnlyCollection<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

/// <summary>App links in emails, rooted at the web app's address (taken from the password reset page).</summary>
public sealed class AppLinks(IOptions<AuthExperienceOptions> options) : IAppLinks
{
    public string? Absolute(string path) =>
        Uri.TryCreate(options.Value.PasswordResetUrl, UriKind.Absolute, out var reset)
            ? new Uri(new Uri(reset.GetLeftPart(UriPartial.Authority)), path).ToString()
            : null;
}

// Accounts are not tenant data: these reads go straight to the identity tables, by client or coach.
public sealed class RecipientDirectory(CoachHubDbContext dbContext) : IRecipientDirectory
{
    public async Task<IReadOnlyList<NotificationRecipient>> ClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<User>().AsNoTracking().Where(x => x.ClientId == clientId && x.IsActive)
            .Select(x => new NotificationRecipient(x.Id, x.Email)).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationRecipient>> CoachAsync(Guid coachId, CancellationToken cancellationToken) =>
        await dbContext.Set<User>().AsNoTracking().Where(x => x.CoachId == coachId && x.ClientId == null && x.IsActive)
            .Select(x => new NotificationRecipient(x.Id, x.Email)).ToArrayAsync(cancellationToken);

    public Task<string?> ClientNameAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (string?)x.Name).SingleOrDefaultAsync(cancellationToken);
}

public sealed class InboxRepository(CoachHubDbContext dbContext) : IInboxRepository
{
    private IQueryable<Notification> Mine(Guid userId) =>
        dbContext.Set<Notification>().Where(x => x.Channel == NotificationChannel.InApp && x.RecipientUserId == userId);

    public async Task<PagedResult<Notification>> ListAsync(Guid userId, InboxQuery query, CancellationToken cancellationToken)
    {
        var items = Mine(userId).AsNoTracking();
        if (query.UnreadOnly) items = items.Where(x => x.ReadAt == null);
        var ordered = items.OrderByDescending(x => x.ScheduledAt).ThenBy(x => x.Id);
        var total = await ordered.LongCountAsync(cancellationToken);
        var page = await ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<Notification>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        Mine(userId).CountAsync(x => x.ReadAt == null, cancellationToken);

    public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        Mine(userId).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> UnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        await Mine(userId).Where(x => x.ReadAt == null).ToArrayAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, string eventKey, bool unreadOnly, CancellationToken cancellationToken) =>
        Mine(userId).AnyAsync(x => x.EventKey == eventKey && (!unreadOnly || x.ReadAt == null), cancellationToken);

    public Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Set<NotificationPreference>().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, NotificationEmailEvents>> EmailPreferencesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await dbContext.Set<NotificationPreference>().AsNoTracking().Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.EmailEvents, cancellationToken);

    public void Add(Notification notification) => dbContext.Set<Notification>().Add(notification);
    public void Add(NotificationPreference preference) => dbContext.Set<NotificationPreference>().Add(preference);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class PlanLookup(CoachHubDbContext dbContext) : IPlanLookup
{
    public async Task<PlanSummary?> FindAsync(PlanKind kind, Guid planId, CancellationToken cancellationToken)
    {
        if (kind == PlanKind.Diet)
        {
            var plan = await dbContext.Set<DietPlan>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId, cancellationToken);
            if (plan is null) return null;
            var status = await dbContext.Set<DietPlanRevision>().AsNoTracking()
                .Where(x => x.PlanId == planId && x.Status != PlanRevisionStatus.Archived)
                .Select(x => (PlanRevisionStatus?)x.Status).FirstOrDefaultAsync(cancellationToken);
            return new PlanSummary(plan.CoachId, plan.ClientId, plan.NameEn, status ?? PlanRevisionStatus.Draft);
        }
        var workout = await dbContext.Set<WorkoutPlan>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId, cancellationToken);
        if (workout is null) return null;
        var current = await dbContext.Set<WorkoutPlanRevision>().AsNoTracking()
            .Where(x => x.PlanId == planId && x.Status != PlanRevisionStatus.Archived)
            .Select(x => (PlanRevisionStatus?)x.Status).FirstOrDefaultAsync(cancellationToken);
        return new PlanSummary(workout.CoachId, workout.ClientId, workout.NameEn, current ?? PlanRevisionStatus.Draft);
    }
}

/// <summary>The time-based events across all coaches; it runs as system work, so the tenant filters are unrestricted.</summary>
public sealed class NotificationEventSource(CoachHubDbContext dbContext) : INotificationEventSource
{
    public async Task<IReadOnlyList<DueCheckIn>> DueCheckInsAsync(DateOnly dueOnOrBefore, CancellationToken cancellationToken) =>
        await dbContext.Set<Client>().AsNoTracking()
            .Where(x => x.IsActive && x.CheckInDueOn != null && x.CheckInDueOn <= dueOnOrBefore &&
                dbContext.Set<CheckInSchedule>().Any(schedule => schedule.ClientId == x.Id))
            .Select(x => new DueCheckIn(x.Id, x.CoachId, x.CheckInDueOn!.Value))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpiredSubscription>> ExpiredSubscriptionsAsync(DateOnly today, DateOnly endedSince, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<Client>().AsNoTracking()
            .Where(x => x.IsActive && x.Subscriptions.Any() && x.Subscriptions.All(s => s.EndDate <= today))
            .Select(x => new { x.Id, x.CoachId, x.Name, EndedOn = x.Subscriptions.Max(s => s.EndDate) })
            .Where(x => x.EndedOn >= endedSince)
            .ToArrayAsync(cancellationToken);
        return rows.Select(x => new ExpiredSubscription(x.Id, x.CoachId, x.Name, x.EndedOn)).ToArray();
    }
}

/// <summary>Scans hourly for the time-based notifications (check-in required, subscription expired).</summary>
public sealed class EventNotificationWorker(IServiceScopeFactory scopes, ILogger<EventNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<EventNotificationScanner>().ScanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Event notification scan failed."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmailEvents).HasConversion<string>().HasMaxLength(300).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
