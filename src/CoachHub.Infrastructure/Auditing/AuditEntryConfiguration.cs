using CoachHub.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Auditing;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EntityType).HasMaxLength(150).IsRequired();
        builder.Property(entry => entry.Operation).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.ActorKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.ActorDisplayName).HasMaxLength(200);
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });
        builder.HasIndex(entry => new { entry.ActorKind, entry.ActorUserId });
    }
}
