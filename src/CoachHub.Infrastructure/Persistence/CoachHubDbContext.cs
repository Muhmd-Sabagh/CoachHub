using CoachHub.Application.Auditing;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Common;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Billing;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Persistence;

public sealed class CoachHubDbContext(
    DbContextOptions<CoachHubDbContext> options,
    IAuditActorAccessor? auditActorAccessor = null)
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
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CaptureAuditEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
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
        });
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<UserRole>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
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
