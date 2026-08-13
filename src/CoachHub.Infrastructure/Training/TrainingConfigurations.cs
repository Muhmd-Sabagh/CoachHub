using CoachHub.Domain.Media;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Training;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.NameEn).HasMaxLength(255).IsRequired();
        builder.Property(exercise => exercise.NameAr).HasMaxLength(255);
        builder.Property(exercise => exercise.YouTubeUrl).HasMaxLength(500);
        builder.Property(exercise => exercise.IsActive).IsRequired();
        builder.HasIndex(exercise => exercise.NameEn);
        builder.HasIndex(exercise => new { exercise.ExerciseCategoryId, exercise.IsActive });
        builder.HasOne<ExerciseCategory>()
            .WithMany()
            .HasForeignKey(exercise => exercise.ExerciseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(exercise => exercise.MediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LegacyExerciseImportRecordConfiguration :
    IEntityTypeConfiguration<LegacyExerciseImportRecord>
{
    public void Configure(EntityTypeBuilder<LegacyExerciseImportRecord> builder)
    {
        builder.ToTable("LegacyExerciseImports");
        builder.HasKey(record => record.Id);
        builder.HasIndex(record => record.LegacyId).IsUnique();
        builder.HasIndex(record => record.ExerciseId).IsUnique();
        builder.Property(record => record.ImportedAt).IsRequired();
        builder.HasOne<Exercise>()
            .WithOne()
            .HasForeignKey<LegacyExerciseImportRecord>(record => record.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}