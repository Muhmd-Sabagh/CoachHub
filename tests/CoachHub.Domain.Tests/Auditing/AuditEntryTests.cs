using CoachHub.Domain.Auditing;

namespace CoachHub.Domain.Tests.Auditing;

public sealed class AuditEntryTests
{
    [Fact]
    public void Entries_record_actor_role_tenant_client_and_changes()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var entry = AuditEntry.Create(
            "Client", entityId, AuditOperation.Update, AuditActorKind.Coach, userId, " Coach ", occurredAt,
            coachId, clientId, """{"Name":{"from":"Old","to":"New"}}""");

        Assert.Equal("Client", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditOperation.Update, entry.Operation);
        Assert.Equal(AuditActorKind.Coach, entry.ActorKind);
        Assert.Equal(userId, entry.ActorUserId);
        Assert.Equal("Coach", entry.ActorDisplayName);
        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.Equal(coachId, entry.CoachId);
        Assert.Equal(clientId, entry.ClientId);
        Assert.Contains("\"from\":\"Old\"", entry.ChangesJson);
    }

    [Theory]
    [InlineData(AuditActorKind.Administrator)]
    [InlineData(AuditActorKind.Coach)]
    [InlineData(AuditActorKind.Client)]
    public void Signed_in_actors_require_a_user_id(AuditActorKind kind)
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            "Client", Guid.NewGuid(), AuditOperation.Update, kind, null, "Someone", DateTimeOffset.UtcNow, null, null, null));
    }

    [Fact]
    public void System_and_public_actors_need_no_user_and_blank_changes_are_stored_as_null()
    {
        var entry = AuditEntry.Create(
            "Notification", Guid.NewGuid(), AuditOperation.Update, AuditActorKind.System, null, null, DateTimeOffset.UtcNow,
            null, null, " ");

        Assert.Null(entry.ChangesJson);
        Assert.Null(entry.CoachId);
    }

    [Fact]
    public void Empty_coach_or_client_identifiers_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            "Client", null, AuditOperation.Create, AuditActorKind.System, null, null, DateTimeOffset.UtcNow, Guid.Empty, null, null));
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            "Client", null, AuditOperation.Create, AuditActorKind.System, null, null, DateTimeOffset.UtcNow, null, Guid.Empty, null));
    }
}
