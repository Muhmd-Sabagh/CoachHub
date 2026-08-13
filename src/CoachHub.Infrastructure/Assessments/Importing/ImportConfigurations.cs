using CoachHub.Domain.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Assessments.Importing;

public sealed class FormImportProfileConfiguration : IEntityTypeConfiguration<FormImportProfile>
{
    public void Configure(EntityTypeBuilder<FormImportProfile> builder)
    {
        builder.ToTable("FormImportProfiles");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SheetName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.FormCodeHeader).HasMaxLength(500).IsRequired();
        builder.Property(item => item.TimestampHeader).HasMaxLength(500).IsRequired();
        builder.Property(item => item.ExternalIdHeader).HasMaxLength(500);
        builder.HasIndex(item => new { item.FormDefinitionId, item.Name }).IsUnique();
        builder.HasOne<FormDefinition>().WithMany().HasForeignKey(item => item.FormDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FormImportColumnMappingConfiguration : IEntityTypeConfiguration<FormImportColumnMapping>
{
    public void Configure(EntityTypeBuilder<FormImportColumnMapping> builder)
    {
        builder.ToTable("FormImportColumnMappings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ExternalColumnKey).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Header).HasMaxLength(500).IsRequired();
        builder.HasIndex(item => new { item.FormImportProfileId, item.ExternalColumnKey }).IsUnique();
        builder.HasIndex(item => new { item.FormImportProfileId, item.QuestionStableKey }).IsUnique();
        builder.HasOne<FormImportProfile>().WithMany().HasForeignKey(item => item.FormImportProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}