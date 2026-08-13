using CoachHub.Domain.Common;

namespace CoachHub.Domain.Auditing;

public enum AuditOperation
{
    Create,
    Update,
    Delete
}

public enum AuditActorKind
{
    Administrator,
    PublicClient,
    System
}

public sealed class AuditEntry : Entity
{
    private AuditEntry() { }

    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public AuditOperation Operation { get; private set; }
    public AuditActorKind ActorKind { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEntry Create(
        string entityType,
        Guid? entityId,
        AuditOperation operation,
        AuditActorKind actorKind,
        Guid? actorUserId,
        string? actorDisplayName,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Audited entity type is required.", nameof(entityType));
        }

        if (actorKind == AuditActorKind.Administrator && !actorUserId.HasValue)
        {
            throw new ArgumentException(
                "Administrator audit actors require a user identifier.",
                nameof(actorUserId));
        }

        return new AuditEntry
        {
            EntityType = Limit(entityType, 150),
            EntityId = entityId,
            Operation = operation,
            ActorKind = actorKind,
            ActorUserId = actorUserId,
            ActorDisplayName = string.IsNullOrWhiteSpace(actorDisplayName)
                ? null
                : Limit(actorDisplayName.Trim(), 200),
            OccurredAt = occurredAt
        };
    }

    private static string Limit(string value, int maximumLength) =>
        value.Trim().Length <= maximumLength
            ? value.Trim()
            : value.Trim()[..maximumLength];
}
