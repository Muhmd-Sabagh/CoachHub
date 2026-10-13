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
        builder.Property(exercise => exercise.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(exercise => exercise.InstructionsEn).HasMaxLength(Exercise.MaximumTextLength);
        builder.Property(exercise => exercise.InstructionsAr).HasMaxLength(Exercise.MaximumTextLength);
        builder.Property(exercise => exercise.CoachingCuesEn).HasMaxLength(Exercise.MaximumTextLength);
        builder.Property(exercise => exercise.CoachingCuesAr).HasMaxLength(Exercise.MaximumTextLength);
        builder.Property(exercise => exercise.CommonMistakesEn).HasMaxLength(Exercise.MaximumTextLength);
        builder.Property(exercise => exercise.CommonMistakesAr).HasMaxLength(Exercise.MaximumTextLength);
        builder.Ignore(exercise => exercise.SecondaryMuscleGroupIds);
        builder.Ignore(exercise => exercise.EquipmentIds);
        builder.HasIndex(exercise => exercise.PrimaryMuscleGroupId);
        builder.HasOne<MuscleGroup>()
            .WithMany()
            .HasForeignKey(exercise => exercise.PrimaryMuscleGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(exercise => exercise.SecondaryMuscles).WithOne()
            .HasForeignKey(link => link.ExerciseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(exercise => exercise.SecondaryMuscles).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(exercise => exercise.Equipment).WithOne()
            .HasForeignKey(link => link.ExerciseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(exercise => exercise.Equipment).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
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

public sealed class ExerciseSecondaryMuscleConfiguration : IEntityTypeConfiguration<ExerciseSecondaryMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseSecondaryMuscle> builder)
    {
        builder.ToTable("ExerciseSecondaryMuscles");
        builder.HasKey(link => link.Id);
        builder.HasIndex(link => new { link.ExerciseId, link.MuscleGroupId }).IsUnique();
        builder.HasIndex(link => link.MuscleGroupId);
        builder.HasOne<MuscleGroup>().WithMany().HasForeignKey(link => link.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExerciseEquipmentConfiguration : IEntityTypeConfiguration<ExerciseEquipment>
{
    public void Configure(EntityTypeBuilder<ExerciseEquipment> builder)
    {
        builder.ToTable("ExerciseEquipment");
        builder.HasKey(link => link.Id);
        builder.HasIndex(link => new { link.ExerciseId, link.EquipmentId }).IsUnique();
        builder.HasIndex(link => link.EquipmentId);
        builder.HasOne<Equipment>().WithMany().HasForeignKey(link => link.EquipmentId).OnDelete(DeleteBehavior.Restrict);
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