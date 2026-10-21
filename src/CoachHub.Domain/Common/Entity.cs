namespace CoachHub.Domain.Common;

public abstract class Entity
{
    private List<IDomainEvent>? _domainEvents;

    public Guid Id { get; protected init; } = Guid.NewGuid();

    /// <summary>Records something that happened; dispatched after the change is saved (P14).</summary>
    protected void Raise(IDomainEvent domainEvent) => (_domainEvents ??= []).Add(domainEvent);

    /// <summary>Hands over the raised events once, so each is dispatched a single time.</summary>
    public IReadOnlyList<IDomainEvent> TakeDomainEvents()
    {
        if (_domainEvents is not { Count: > 0 } events) return [];
        _domainEvents = null;
        return events;
    }
}

/// <summary>
/// Something that happened in the domain, such as a plan version becoming active or a chat message being sent.
/// Handlers run after the change is saved, so they never see uncommitted state.
/// </summary>
public interface IDomainEvent;
