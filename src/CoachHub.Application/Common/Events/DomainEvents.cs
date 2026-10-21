using CoachHub.Domain.Common;

namespace CoachHub.Application.Common.Events;

/// <summary>Reacts to a domain event after the change that raised it is saved (P14).</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Runs the handlers of saved events. A failing handler is logged and never undoes or fails the change that raised
/// the event: notifications are side effects.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken);
}
