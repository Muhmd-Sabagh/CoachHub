using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Events;
using CoachHub.Infrastructure.Auditing;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Common;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.Media;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Persistence;

public sealed class CoachHubDbContext(
    DbContextOptions<CoachHubDbContext> options,
    IAuditActorAccessor? auditActorAccessor = null,
    ICurrentUser? currentUser = null,
    IDomainEventDispatcher? domainEvents = null)
    : IdentityDbContext<
        User,
        Role,
        Guid,
        IdentityUserClaim<Guid>,
        UserRole,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>(options)
{
    // Read by the tenant query filters on every query; see TenantModel. A context without a
    // current user (design time, background work, tests) is unrestricted.
    internal bool TenantUnrestricted => currentUser is null || currentUser.Scope == TenantScope.Unrestricted;
    internal bool TenantClientScoped => currentUser?.Scope == TenantScope.Client;
    internal Guid TenantCoachId => currentUser?.CoachId ?? Guid.Empty;
    internal Guid TenantClientId => currentUser?.ClientId ?? Guid.Empty;
    internal bool TenantSignedIn => currentUser?.Scope is TenantScope.Coach or TenantScope.Client;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOwnership();
        EnforceArchivedPlanVersionsAsync(async: false, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        CaptureAuditEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceTenantOwnership();
        await EnforceArchivedPlanVersionsAsync(async: true, cancellationToken);
        CaptureAuditEntries();
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await DispatchDomainEventsAsync(cancellationToken);
        return saved;
    }

    private int _dispatchDepth;

    /// <summary>
    /// P14: hands the events raised by the saved entities to their handlers. Handlers may save again (their own
    /// notifications); the depth limit stops a handler loop.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        if (domainEvents is null || _dispatchDepth > 3) return;
        var events = ChangeTracker.Entries<Entity>().SelectMany(entry => entry.Entity.TakeDomainEvents()).ToArray();
        if (events.Length == 0) return;
        _dispatchDepth++;
        try
        {
            await domainEvents.DispatchAsync(events, cancellationToken);
        }
        finally
        {
            _dispatchDepth--;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoachHubDbContext).Assembly);

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
            builder.HasIndex(user => user.ClientId).IsUnique().HasFilter("[ClientId] IS NOT NULL");
            builder.HasOne<Client>().WithOne().HasForeignKey<User>(user => user.ClientId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(user => user.CoachId).IsUnique().HasFilter("[CoachId] IS NOT NULL");
            builder.HasOne<Coach>().WithOne().HasForeignKey<User>(user => user.CoachId).OnDelete(DeleteBehavior.Restrict);
            builder.Property(user => user.PreferredLanguage).HasMaxLength(5);
            builder.Property(user => user.PhoneNumber).HasMaxLength(30);
            builder.HasOne<MediaAsset>().WithMany().HasForeignKey(user => user.AvatarMediaId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<UserRole>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        TenantModel.Apply(modelBuilder, this);
    }

    /// <summary>
    /// Defense in depth behind the query filters: a coach may only write rows of their own coach,
    /// a client only rows of their own (their client record, or rows whose ClientId is theirs),
    /// and anonymous callers may not write coach-owned rows at all.
    /// </summary>
    private void EnforceTenantOwnership()
    {
        if (TenantUnrestricted) return;
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is AuditEntry) continue; // Written by the system on the caller's behalf.
            Guid? owner = entry.Entity switch
            {
                ITenantOwned owned => owned.CoachId,
                IOptionalTenantOwned owned => owned.CoachId,
                _ => Guid.Empty
            };
            if (owner == Guid.Empty) continue;
            var original = entry.State == EntityState.Modified &&
                entry.Metadata.FindProperty(nameof(ITenantOwned.CoachId)) is not null
                    ? entry.Property(nameof(ITenantOwned.CoachId)).OriginalValue as Guid?
                    : owner;
            if (TenantCoachId == Guid.Empty || owner != TenantCoachId || original != TenantCoachId)
            {
                throw new ForbiddenException("The record belongs to another coach.");
            }
            if (TenantClientScoped && !BelongsToCurrentClient(entry))
            {
                throw new ForbiddenException("The record belongs to another client.");
            }
        }
    }

    /// <summary>
    /// D4: an archived plan version and its content are read-only. Nothing may add to, change or remove
    /// them; the one exception is deleting the whole plan, which removes its history with it.
    /// </summary>
    private async ValueTask EnforceArchivedPlanVersionsAsync(bool async, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var changed = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => entry.Entity is PlanRevision or IPlanRevisionContent)
            .ToList();
        if (changed.Count == 0) return;
        var deletedPlans = ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Deleted && entry.Entity is DietPlan or WorkoutPlan)
            .Select(entry => ((Entity)entry.Entity).Id)
            .ToHashSet();

        var versions = new Dictionary<Guid, (Guid PlanId, PlanRevisionStatus Status)>();
        foreach (var entry in ChangeTracker.Entries<PlanRevision>())
        {
            var status = entry.State == EntityState.Added
                ? entry.Entity.Status
                : (PlanRevisionStatus)entry.Property(nameof(PlanRevision.Status)).OriginalValue!;
            versions[entry.Entity.Id] = (entry.Entity.PlanId, status);
        }

        var contentVersions = changed
            .Where(entry => entry.Entity is IPlanRevisionContent)
            .Select(entry => (Entry: entry, RevisionIds: new[]
            {
                ((IPlanRevisionContent)entry.Entity).RevisionId,
                entry.State == EntityState.Added
                    ? ((IPlanRevisionContent)entry.Entity).RevisionId
                    : (Guid)entry.Property(nameof(IPlanRevisionContent.RevisionId)).OriginalValue!
            }.Distinct().ToArray()))
            .ToList();
        var unknown = contentVersions.SelectMany(x => x.RevisionIds).Where(id => !versions.ContainsKey(id)).Distinct().ToArray();
        if (unknown.Length > 0)
        {
            var diet = Set<DietPlanRevision>().AsNoTracking().Where(x => unknown.Contains(x.Id))
                .Select(x => new { x.Id, x.PlanId, x.Status });
            var workout = Set<WorkoutPlanRevision>().AsNoTracking().Where(x => unknown.Contains(x.Id))
                .Select(x => new { x.Id, x.PlanId, x.Status });
            var found = async
                ? (await diet.ToListAsync(cancellationToken)).Concat(await workout.ToListAsync(cancellationToken))
                : diet.ToList().Concat(workout.ToList());
            foreach (var version in found) versions[version.Id] = (version.PlanId, version.Status);
        }

        foreach (var entry in changed)
        {
            var revisionIds = entry.Entity is PlanRevision revision
                ? [revision.Id]
                : contentVersions.Single(x => x.Entry == entry).RevisionIds;
            foreach (var revisionId in revisionIds)
            {
                if (!versions.TryGetValue(revisionId, out var version) || version.Status != PlanRevisionStatus.Archived) continue;
                if (entry.State == EntityState.Deleted && deletedPlans.Contains(version.PlanId)) continue;
                throw new ConflictException("Archived plan versions are read-only. Save your changes as a new version instead.");
            }
        }
    }

    private bool BelongsToCurrentClient(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is Client client) return client.Id == TenantClientId;
        if (entry.Metadata.FindProperty("ClientId") is null) return false;
        var property = entry.Property("ClientId");
        return Equals(property.CurrentValue, TenantClientId) &&
            (entry.State != EntityState.Modified || Equals(property.OriginalValue, TenantClientId));
    }

    private void CaptureAuditEntries()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<SubscriptionRenewal>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Subscription renewal transactions are append-only and cannot be modified or deleted.");
        }
        if (ChangeTracker.Entries<Refund>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Refund transactions are append-only.");
        if (ChangeTracker.Entries<DeliveredPlan>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Delivered plan snapshots are append-only.");
        if (ChangeTracker.Entries<AuditEntry>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Audit entries are append-only and cannot be modified or deleted.");
        }

        var entries = AuditCapture.Capture(
            ChangeTracker,
            AuditActor,
            TenantSignedIn ? TenantCoachId : null,
            TenantClientScoped ? TenantClientId : null,
            DateTimeOffset.UtcNow);
        if (entries.Count > 0)
        {
            Set<AuditEntry>().AddRange(entries);
        }
    }

    private AuditActor AuditActor => auditActorAccessor?.Current ?? new AuditActor(AuditActorKind.System);

    /// <summary>
    /// Records an operation that bypasses change tracking (bulk updates), with the current actor.
    /// Saved with the next <see cref="SaveChangesAsync(CancellationToken)"/>.
    /// </summary>
    internal void RecordAudit(
        string entityType,
        Guid? entityId,
        AuditOperation operation,
        Guid? coachId,
        Guid? clientId,
        IReadOnlyDictionary<string, (object? From, object? To)> changes) =>
        Set<AuditEntry>().Add(AuditCapture.Record(
            AuditActor, entityType, entityId, operation, coachId, clientId, changes, DateTimeOffset.UtcNow));
}
