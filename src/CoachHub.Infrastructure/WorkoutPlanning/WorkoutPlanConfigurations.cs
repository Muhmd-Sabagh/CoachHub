using CoachHub.Domain.Clients;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.WorkoutPlanning;

public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlan>
{
    public void Configure(EntityTypeBuilder<WorkoutPlan> builder)
    {
        builder.ToTable("WorkoutPlans"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(255).IsRequired(); builder.Property(x => x.NameAr).HasMaxLength(255);
        builder.Property(x => x.CreatedAt).IsRequired(); builder.HasIndex(x => x.ClientId);
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class WorkoutPlanRevisionConfiguration : IEntityTypeConfiguration<WorkoutPlanRevision>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanRevision> builder)
    {
        builder.ToTable("WorkoutPlanRevisions");
        PlanRevisionMapping.Configure(builder);
        builder.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutPlanNoteConfiguration : IEntityTypeConfiguration<WorkoutPlanNote>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanNote> builder)
    {
        builder.ToTable("WorkoutPlanNotes"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.RevisionId, x.Order }).IsUnique();
        builder.HasOne<WorkoutPlanRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDay>
{
    public void Configure(EntityTypeBuilder<WorkoutDay> builder)
    {
        builder.ToTable("WorkoutDays"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired(); builder.Property(x => x.NameAr).HasMaxLength(100);
        builder.Property(x => x.Subtitle).HasMaxLength(100); builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.RevisionId, x.Order }).IsUnique();
        builder.HasOne<WorkoutPlanRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutExerciseConfiguration : IEntityTypeConfiguration<WorkoutExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutExercise> builder)
    {
        builder.ToTable("WorkoutExercises"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Block).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.WorkoutDayId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<WorkoutDay>().WithMany().HasForeignKey(x => x.WorkoutDayId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        // The day already cascades to its exercises; a second cascade path through the group is not allowed.
        builder.HasOne<WorkoutSetGroup>().WithMany().HasForeignKey(x => x.SetGroupId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WorkoutSetGroupConfiguration : IEntityTypeConfiguration<WorkoutSetGroup>
{
    public void Configure(EntityTypeBuilder<WorkoutSetGroup> builder)
    {
        builder.ToTable("WorkoutSetGroups"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<WorkoutDay>().WithMany().HasForeignKey(x => x.WorkoutDayId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PrescribedSetConfiguration : IEntityTypeConfiguration<PrescribedSet>
{
    public void Configure(EntityTypeBuilder<PrescribedSet> builder)
    {
        builder.ToTable("WorkoutPrescribedSets", table =>
        {
            table.HasCheckConstraint("CK_WorkoutPrescribedSets_Reps", "[RepsMax] IS NULL OR ([RepsMin] IS NOT NULL AND [RepsMin] <= [RepsMax])");
            table.HasCheckConstraint("CK_WorkoutPrescribedSets_Tempo",
                "([TempoEccentric] IS NULL AND [TempoBottomPause] IS NULL AND [TempoConcentric] IS NULL AND [TempoTopPause] IS NULL) OR " +
                "([TempoEccentric] IS NOT NULL AND [TempoBottomPause] IS NOT NULL AND [TempoConcentric] IS NOT NULL AND [TempoTopPause] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LoadKg).HasPrecision(7, 2);
        builder.Property(x => x.Rpe).HasPrecision(3, 1);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Ignore(x => x.Tempo);
        builder.Ignore(x => x.Prescription);
        builder.HasIndex(x => new { x.WorkoutExerciseId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<WorkoutExercise>().WithMany().HasForeignKey(x => x.WorkoutExerciseId).OnDelete(DeleteBehavior.Cascade);
    }
}
