using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Common;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Coaches;
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
    ICurrentUser? currentUser = null)
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
        CaptureAuditEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceTenantOwnership();
        CaptureAuditEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
        var trackedAuditEntries = ChangeTracker.Entries<AuditEntry>().ToArray();
        if (trackedAuditEntries.Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Audit entries are append-only and cannot be modified or deleted.");
        }
        if (trackedAuditEntries.Any(entry => entry.State == EntityState.Added))
        {
            return;
        }

        var actor = auditActorAccessor?.Current ?? new AuditActor(AuditActorKind.System);
        var occurredAt = DateTimeOffset.UtcNow;
        var entries = ChangeTracker.Entries()
            .Where(entry => entry.Entity is not AuditEntry && entry.State is
                EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => AuditEntry.Create(
                entry.Metadata.ClrType.Name,
                EntityId(entry),
                Operation(entry.State),
                actor.Kind,
                actor.UserId,
                actor.DisplayName,
                occurredAt))
            .ToArray();

        if (entries.Length > 0)
        {
            Set<AuditEntry>().AddRange(entries);
        }
    }

    private static Guid? EntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is Entity entity)
        {
            return entity.Id;
        }

        var key = entry.Metadata.FindPrimaryKey();
        return key?.Properties.Count == 1 &&
            entry.Property(key.Properties[0].Name).CurrentValue is Guid id
                ? id
                : null;
    }

    private static AuditOperation Operation(EntityState state) => state switch
    {
        EntityState.Added => AuditOperation.Create,
        EntityState.Modified => AuditOperation.Update,
        EntityState.Deleted => AuditOperation.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
