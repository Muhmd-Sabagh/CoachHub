using System.Text.Json;
using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Auditing;

/// <summary>What the audit trail captures (D2): previous and new values, never secrets, no noise.</summary>
public sealed class AuditCaptureTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CoachId = Guid.NewGuid();

    [Fact]
    public async Task An_update_records_previous_and_new_values_of_changed_properties_only()
    {
        await using var db = Context();
        var package = Package.Create(CoachId, "Starter", null, "Four weeks");
        db.Add(package);
        await db.SaveChangesAsync();

        package.Update("Premium", null, "Four weeks", true);
        await db.SaveChangesAsync();

        var changes = Changes(await Latest(db, nameof(Package), AuditOperation.Update));
        Assert.Equal("Starter", changes["NameEn"].GetProperty("from").GetString());
        Assert.Equal("Premium", changes["NameEn"].GetProperty("to").GetString());
        Assert.False(changes.ContainsKey("Description"));
        Assert.False(changes.ContainsKey("CoachId"));
    }

    [Fact]
    public async Task A_create_records_new_values_and_a_delete_records_previous_values()
    {
        await using var db = Context();
        var package = Package.Create(CoachId, "Short lived", null, null);
        db.Add(package);
        await db.SaveChangesAsync();
        db.Remove(package);
        await db.SaveChangesAsync();

        var created = Changes(await Latest(db, nameof(Package), AuditOperation.Create));
        var deleted = Changes(await Latest(db, nameof(Package), AuditOperation.Delete));
        Assert.Equal("Short lived", created["NameEn"].GetProperty("to").GetString());
        Assert.False(created["NameEn"].TryGetProperty("from", out _));
        Assert.Equal("Short lived", deleted["NameEn"].GetProperty("from").GetString());
        Assert.Equal(CoachId, (await Latest(db, nameof(Package), AuditOperation.Delete)).CoachId);
    }

    [Fact]
    public async Task Secrets_are_never_captured()
    {
        await using var db = Context();
        var user = new User
        {
            UserName = "secret@coachhub.test", Email = "secret@coachhub.test", DisplayName = "Secret",
            PasswordHash = "HASHED-PASSWORD", SecurityStamp = "STAMP-VALUE", ConcurrencyStamp = "CONCURRENCY"
        };
        db.Add(user);
        db.Add(UserSession.Create(Guid.NewGuid(), "REFRESH-HASH", "Agent", null,
            DateTimeOffset.UtcNow, TimeSpan.FromDays(1), TimeSpan.FromDays(2)));
        var client = Client.Create(CoachId, "CLIENTCD", "Audited client", null, null, DateOnly.FromDateTime(DateTime.UtcNow));
        db.Add(client);
        db.Add(Notification.Schedule(CoachId, client.Id, NotificationChannel.Email, "a@b.test", "Reset",
            "Reset link: https://example.test/reset?token=ONE-TIME-TOKEN", DateTimeOffset.UtcNow, isAccountMessage: true));
        await db.SaveChangesAsync();

        var trail = string.Join("\n", await db.Set<AuditEntry>().Select(entry => entry.ChangesJson).ToArrayAsync());
        Assert.Contains("Audited client", trail);
        foreach (var secret in new[] { "HASHED-PASSWORD", "STAMP-VALUE", "CONCURRENCY", "REFRESH-HASH", "ONE-TIME-TOKEN" })
            Assert.DoesNotContain(secret, trail);
    }

    [Fact]
    public async Task Bookkeeping_only_updates_are_not_audited_but_meaningful_session_events_are()
    {
        await using var db = Context();
        var session = UserSession.Create(Guid.NewGuid(), "hash-1", "Agent", "10.0.0.1",
            DateTimeOffset.UtcNow, TimeSpan.FromDays(1), TimeSpan.FromDays(2));
        db.Add(session);
        await db.SaveChangesAsync();
        var afterSignIn = await db.Set<AuditEntry>().CountAsync(entry => entry.EntityType == nameof(UserSession));

        session.Rotate("hash-2", "Agent", "10.0.0.1", DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromDays(1));
        await db.SaveChangesAsync();
        Assert.Equal(afterSignIn, await db.Set<AuditEntry>().CountAsync(entry => entry.EntityType == nameof(UserSession)));

        session.Revoke(DateTimeOffset.UtcNow.AddMinutes(2), SessionEndReason.SignedOut);
        await db.SaveChangesAsync();
        var revoked = Changes(await Latest(db, nameof(UserSession), AuditOperation.Update));
        Assert.Equal("SignedOut", revoked["EndReason"].GetProperty("to").GetString());
        Assert.Equal(1, afterSignIn);
    }

    [Fact]
    public async Task Long_values_are_truncated()
    {
        await using var db = Context();
        db.Add(new User { UserName = "long@coachhub.test", Email = "long@coachhub.test", DisplayName = new string('x', 5000) });
        await db.SaveChangesAsync();

        var created = Changes(await Latest(db, nameof(User), AuditOperation.Create));
        Assert.Equal(1001, created["DisplayName"].GetProperty("to").GetString()!.Length);
    }

    [Fact]
    public async Task The_actor_role_and_tenant_come_from_the_caller()
    {
        var clientId = Guid.NewGuid();
        await using var db = Context(
            new AuditActor(AuditActorKind.Client, UserId, "Client user"),
            new FixedUser(TenantScope.Client, CoachId, clientId));
        // A row with no coach or client of its own takes the caller's tenant.
        db.Add(Currency.Create("TST", "Test currency", null));
        await db.SaveChangesAsync();

        var entry = await Latest(db, nameof(Currency), AuditOperation.Create);
        Assert.Equal(AuditActorKind.Client, entry.ActorKind);
        Assert.Equal(UserId, entry.ActorUserId);
        Assert.Equal(CoachId, entry.CoachId);
        Assert.Equal(clientId, entry.ClientId);
    }

    private static CoachHubDbContext Context(AuditActor? actor = null, ICurrentUser? user = null) => new(
        new DbContextOptionsBuilder<CoachHubDbContext>().UseInMemoryDatabase("audit-capture-" + Guid.NewGuid()).Options,
        new FixedActor(actor ?? new AuditActor(AuditActorKind.Administrator, UserId, "Admin")),
        user);

    private static async Task<AuditEntry> Latest(CoachHubDbContext db, string entityType, AuditOperation operation) =>
        (await db.Set<AuditEntry>().Where(entry => entry.EntityType == entityType && entry.Operation == operation)
            .ToArrayAsync()).OrderBy(entry => entry.OccurredAt).Last();

    private static Dictionary<string, JsonElement> Changes(AuditEntry entry) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.ChangesJson!)!;

    private sealed class FixedActor(AuditActor actor) : IAuditActorAccessor
    {
        public AuditActor Current => actor;
    }

    private sealed class FixedUser(TenantScope scope, Guid? coachId, Guid? clientId) : ICurrentUser
    {
        public Guid? UserId => AuditCaptureTests.UserId;
        public string? DisplayName => "Test";
        public string? Role => scope.ToString();
        public TenantScope Scope => scope;
        public Guid? CoachId => coachId;
        public Guid? ClientId => clientId;
        public bool IsAdministrator => false;
    }
}
