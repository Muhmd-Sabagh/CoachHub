using CoachHub.Domain.Auditing;

namespace CoachHub.Domain.Tests.Auditing;

public sealed class AuditEntryTests
{
    [Fact]
    public void Administrator_entries_require_identity_and_capture_metadata_only()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var entry = AuditEntry.Create(
            "Client",
            entityId,
            AuditOperation.Update,
            AuditActorKind.Administrator,
            userId,
            "Coach",
            occurredAt);

        Assert.Equal("Client", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditOperation.Update, entry.Operation);
        Assert.Equal(AuditActorKind.Administrator, entry.ActorKind);
        Assert.Equal(userId, entry.ActorUserId);
        Assert.Equal("Coach", entry.ActorDisplayName);
        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.DoesNotContain(
            entry.GetType().GetProperties(),
            property => property.Name.Contains("Value", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Changes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Administrator_without_user_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            "Client",
            Guid.NewGuid(),
            AuditOperation.Update,
            AuditActorKind.Administrator,
            null,
            "Coach",
            DateTimeOffset.UtcNow));
    }
}
