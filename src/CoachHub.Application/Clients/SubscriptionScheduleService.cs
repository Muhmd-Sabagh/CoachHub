using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

/// <summary>Reads and saves the subscriptions the scheduled pass changes (P22). Runs as system work, across coaches.</summary>
public interface ISubscriptionScheduleRepository
{
    /// <summary>The overdue-suspension rule: days after the due date, or null when it is off.</summary>
    Task<int?> OverdueSuspensionDaysAsync(CancellationToken cancellationToken);

    /// <summary>Suspended subscriptions whose resume date is on or before <paramref name="today"/> (tracked).</summary>
    Task<IReadOnlyList<Subscription>> ListResumeDueAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// Subscriptions with an issued or partly paid invoice that fell due on or before <paramref name="dueBy"/> (tracked).
    /// </summary>
    Task<IReadOnlyList<Subscription>> ListWithOverdueInvoicesAsync(DateTimeOffset dueBy, CancellationToken cancellationToken);

    /// <summary>Subscriptions the platform suspended for an overdue invoice (tracked).</summary>
    Task<IReadOnlyList<Subscription>> ListSuspendedForOverdueAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The scheduled subscription pass (P22, hourly): suspensions with a resume date that has come end; with the
/// Administrator's rule on, subscriptions with an invoice overdue by the set days are suspended, and those it
/// suspended run again once no invoice is overdue. Every change is audited like any other.
/// </summary>
public sealed class SubscriptionScheduleService(ISubscriptionScheduleRepository repository, TimeProvider timeProvider)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        foreach (var subscription in await repository.ListResumeDueAsync(today, cancellationToken))
            if (subscription.ResumeIsDue(today)) subscription.Resume();

        var overdue = Array.Empty<Guid>();
        if (await repository.OverdueSuspensionDaysAsync(cancellationToken) is { } days)
        {
            var subscriptions = await repository.ListWithOverdueInvoicesAsync(now.AddDays(-days), cancellationToken);
            foreach (var subscription in subscriptions) subscription.SuspendForOverdueInvoice(today, now);
            overdue = subscriptions.Select(x => x.Id).ToArray();
        }
        // With the rule off nothing is overdue, so the platform's own suspensions end too.
        foreach (var subscription in await repository.ListSuspendedForOverdueAsync(cancellationToken))
            if (!overdue.Contains(subscription.Id)) subscription.Resume();
        await repository.SaveChangesAsync(cancellationToken);
    }
}
