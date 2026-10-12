using CoachHub.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Persistence;

/// <summary>Shared mapping of diet and workout plan versions (D4).</summary>
internal static class PlanRevisionMapping
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : PlanRevision
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Ignore(x => x.IsArchived);
        builder.HasIndex(x => new { x.PlanId, x.VersionNumber }).IsUnique();
        // Exactly one current (Draft or Active) version per plan; every other version is archived.
        builder.HasIndex(x => x.PlanId, $"IX_{typeof(T).Name}s_PlanId_Current").IsUnique().HasFilter("[Status] <> 'Archived'");
        builder.ToTable(table => table.HasCheckConstraint(
            $"CK_{typeof(T).Name}s_Dates", "[EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]"));
    }
}
