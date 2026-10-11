using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// The save guard behind the query filters: a client may write only rows of their own, never
/// another client's or their coach's library, even within their coach's tenant.
/// </summary>
public sealed class TenantWriteGuardTests
{
    private static readonly Guid CoachId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();

    [Fact]
    public async Task A_client_may_write_their_own_media()
    {
        await using var db = Context(new GuardUser(TenantScope.Client, CoachId, ClientId));
        db.Add(Media(CoachId, ClientId));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_client_may_not_write_another_clients_media_or_the_coach_library()
    {
        await using var db = Context(new GuardUser(TenantScope.Client, CoachId, ClientId));

        db.Add(Media(CoachId, Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.Add(Media(CoachId, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_coach_may_not_write_platform_media_or_another_coachs_media()
    {
        await using var db = Context(new GuardUser(TenantScope.Coach, CoachId, null));

        db.Add(Media(null, null));
        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.Add(Media(Guid.NewGuid(), null));
        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
    }

    private static MediaAsset Media(Guid? coachId, Guid? clientId) =>
        MediaAsset.Create("key-" + Guid.NewGuid().ToString("N"), "a.png", "image/png", 8, DateTimeOffset.UtcNow,
            coachId, clientId, null);

    private static CoachHubDbContext Context(ICurrentUser user) => new(
        new DbContextOptionsBuilder<CoachHubDbContext>().UseInMemoryDatabase("guard-" + Guid.NewGuid()).Options,
        auditActorAccessor: null,
        currentUser: user);

    private sealed class GuardUser(TenantScope scope, Guid? coachId, Guid? clientId) : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();
        public string? DisplayName => "Guard test";
        public string? Role => scope.ToString();
        public TenantScope Scope => scope;
        public Guid? CoachId => coachId;
        public Guid? ClientId => clientId;
        public bool IsAdministrator => false;
    }
}
