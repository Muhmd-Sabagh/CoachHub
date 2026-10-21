using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Events;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Notifications;

/// <summary>
/// Plan events (§42, owner decision 2026-10-22): a plan whose version 1 becomes Active, or an active plan given to a
/// client, is a new diet or workout; a later version becoming Active is a new program; saving the active version in
/// place is "program updated", at most once an hour per plan. Drafts and plans without a client notify nobody.
/// </summary>
public sealed class PlanNotificationHandler(IPlanLookup plans, NotificationCenter center, TimeProvider timeProvider) :
    IDomainEventHandler<PlanVersionActivated>,
    IDomainEventHandler<ActivePlanVersionSaved>,
    IDomainEventHandler<PlanAssigned>
{
    public async Task HandleAsync(PlanVersionActivated domainEvent, CancellationToken cancellationToken)
    {
        if (await plans.FindAsync(domainEvent.Kind, domainEvent.PlanId, cancellationToken) is not { ClientId: { } clientId } plan) return;
        if (domainEvent.VersionNumber == 1)
        {
            await NotifyNewAsync(domainEvent.Kind, domainEvent.PlanId, clientId, plan, cancellationToken);
            return;
        }
        await center.NotifyClientAsync(new EventNotice(NotificationEvent.NewProgram, plan.CoachId, clientId,
            $"New version of {plan.Name}",
            $"Version {domainEvent.VersionNumber} of your {Noun(domainEvent.Kind)} plan is now active.",
            Link(domainEvent.Kind), $"plan:{domainEvent.PlanId}:v{domainEvent.VersionNumber}"), cancellationToken);
    }

    public async Task HandleAsync(ActivePlanVersionSaved domainEvent, CancellationToken cancellationToken)
    {
        if (await plans.FindAsync(domainEvent.Kind, domainEvent.PlanId, cancellationToken) is not { ClientId: { } clientId } plan) return;
        var hour = timeProvider.GetUtcNow().ToString("yyyyMMddHH");
        await center.NotifyClientAsync(new EventNotice(NotificationEvent.ProgramUpdated, plan.CoachId, clientId,
            $"{plan.Name} was updated", $"Your coach updated your {Noun(domainEvent.Kind)} plan.",
            Link(domainEvent.Kind), $"plan:{domainEvent.PlanId}:updated:{hour}"), cancellationToken);
    }

    public async Task HandleAsync(PlanAssigned domainEvent, CancellationToken cancellationToken)
    {
        var plan = await plans.FindAsync(domainEvent.Kind, domainEvent.PlanId, cancellationToken);
        if (plan is null || plan.ClientId != domainEvent.ClientId || plan.CurrentStatus != PlanRevisionStatus.Active) return;
        await NotifyNewAsync(domainEvent.Kind, domainEvent.PlanId, domainEvent.ClientId, plan, cancellationToken);
    }

    // Activation and assignment share the key, so a plan created Active for a client notifies once.
    private Task NotifyNewAsync(PlanKind kind, Guid planId, Guid clientId, PlanSummary plan, CancellationToken cancellationToken) =>
        center.NotifyClientAsync(new EventNotice(kind == PlanKind.Diet ? NotificationEvent.NewDiet : NotificationEvent.NewWorkout,
            plan.CoachId, clientId, kind == PlanKind.Diet ? "New diet plan" : "New workout plan",
            $"Your coach shared a new {Noun(kind)} plan: {plan.Name}.", Link(kind), $"plan:{planId}:client:{clientId}:new"), cancellationToken);

    private static string Noun(PlanKind kind) => kind == PlanKind.Diet ? "diet" : "workout";
    private static string Link(PlanKind kind) => kind == PlanKind.Diet ? "/app/nutrition" : "/app/training";
}

/// <summary>The coach approved a check-in, asked for changes or replied (§42 "check-in reviewed").</summary>
public sealed class CheckInNotificationHandler(NotificationCenter center) : IDomainEventHandler<CheckInReviewed>
{
    public Task HandleAsync(CheckInReviewed domainEvent, CancellationToken cancellationToken)
    {
        var (title, body) = domainEvent.Outcome switch
        {
            CheckInStatus.Approved => ("Check-in approved", "Your coach reviewed and approved your check-in."),
            CheckInStatus.ChangesRequested => ("Your coach asked for changes", "Update your check-in and send it again."),
            _ => ("New reply on your check-in", "Your coach replied to your check-in.")
        };
        return center.NotifyClientAsync(new EventNotice(NotificationEvent.CheckInReviewed, domainEvent.CoachId, domainEvent.ClientId,
            title, body, "/app/forms", null), cancellationToken);
    }
}

/// <summary>
/// The time-based events (§42), run by a background worker: a check-in that is open and not yet sent ("check-in
/// required", once per due date), and a client whose subscriptions all ended in the last week ("subscription
/// expired", to the client and the coach, once per end date).
/// </summary>
public sealed class EventNotificationScanner(INotificationEventSource source, NotificationCenter center, TimeProvider timeProvider)
{
    /// <summary>How long after an end date the expiry is still announced, so a first run does not flood old ones.</summary>
    public const int ExpiryNoticeDays = 7;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        foreach (var due in await source.DueCheckInsAsync(today.AddDays(CheckInService.OpensDaysBefore), cancellationToken))
        {
            await center.NotifyClientAsync(new EventNotice(NotificationEvent.CheckInRequired, due.CoachId, due.ClientId,
                due.DueOn < today ? "Your check-in is overdue" : "Time for your check-in",
                $"Your check-in is due on {due.DueOn:ddd d MMM}.", "/app/forms", $"checkin:{due.DueOn:yyyy-MM-dd}"), cancellationToken);
        }
        foreach (var expired in await source.ExpiredSubscriptionsAsync(today, today.AddDays(-ExpiryNoticeDays), cancellationToken))
        {
            var key = $"subscription:{expired.ClientId}:{expired.EndedOn:yyyy-MM-dd}";
            await center.NotifyClientAsync(new EventNotice(NotificationEvent.SubscriptionExpired, expired.CoachId, expired.ClientId,
                "Your subscription has ended", $"Your subscription ended on {expired.EndedOn:d MMM yyyy}. Contact your coach to renew.",
                "/app", key), cancellationToken);
            await center.NotifyCoachAsync(new EventNotice(NotificationEvent.SubscriptionExpired, expired.CoachId, expired.ClientId,
                $"{expired.ClientName}'s subscription has ended", $"The subscription ended on {expired.EndedOn:d MMM yyyy}.",
                $"/clients/{expired.ClientId}", key), cancellationToken);
        }
    }
}
