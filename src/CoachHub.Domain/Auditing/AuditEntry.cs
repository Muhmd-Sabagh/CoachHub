using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Auditing;

[JsonConverter(typeof(JsonStringEnumConverter<AuditOperation>))]
public enum AuditOperation
{
    Create,
    Update,
    Delete
}

/// <summary>The real role of whoever performed the operation (D2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditActorKind>))]
public enum AuditActorKind
{
    Administrator,
    Coach,
    Client,

    /// <summary>
    /// An anonymous client using the legacy code-based forms (retired in P5). Nothing records it now; it stays so
    /// earlier audit rows, which are append-only, remain readable.
    /// </summary>
    PublicClient,

    /// <summary>Background work such as the notification dispatcher.</summary>
    System
}

/// <summary>
/// One audited change (D2, §58): the entity, the affected coach and client, who did it with which
/// role, the operation, the changed values, and when. Append-only. Tenant-isolated by
/// <see cref="CoachId"/>, so a coach reads only their own tenant's trail.
/// </summary>
public sealed class AuditEntry : Entity, IOptionalTenantOwned
{
    private AuditEntry() { }

    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public AuditOperation Operation { get; private set; }
    public AuditActorKind ActorKind { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The coach (tenant) whose data changed; null for platform data.</summary>
    public Guid? CoachId { get; private set; }

    /// <summary>The affected client, when the change concerns one.</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>
    /// JSON object of property → <c>{ "from": previous, "to": new }</c>: changed properties for an
    /// update, new values for a create, previous values for a delete. Never holds secrets.
    /// </summary>
    public string? ChangesJson { get; private set; }

    public static AuditEntry Create(
        string entityType,
        Guid? entityId,
        AuditOperation operation,
        AuditActorKind actorKind,
        Guid? actorUserId,
        string? actorDisplayName,
        DateTimeOffset occurredAt,
        Guid? coachId,
        Guid? clientId,
        string? changesJson)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Audited entity type is required.", nameof(entityType));
        }

        if (actorKind is AuditActorKind.Administrator or AuditActorKind.Coach or AuditActorKind.Client &&
            !actorUserId.HasValue)
        {
            throw new ArgumentException(
                "Signed-in audit actors require a user identifier.",
                nameof(actorUserId));
        }

        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier cannot be empty.", nameof(clientId));

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
            OccurredAt = occurredAt,
            CoachId = TenantKey.Optional(coachId),
            ClientId = clientId,
            ChangesJson = string.IsNullOrWhiteSpace(changesJson) ? null : changesJson
        };
    }

    private static string Limit(string value, int maximumLength) =>
        value.Trim().Length <= maximumLength
            ? value.Trim()
            : value.Trim()[..maximumLength];
}
