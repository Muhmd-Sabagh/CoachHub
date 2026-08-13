using CoachHub.Domain.Clients;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
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

public sealed class WorkoutPlanNoteConfiguration : IEntityTypeConfiguration<WorkoutPlanNote>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanNote> builder)
    {
        builder.ToTable("WorkoutPlanNotes"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.WorkoutPlanId, x.Order }).IsUnique();
        builder.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => x.WorkoutPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDay>
{
    public void Configure(EntityTypeBuilder<WorkoutDay> builder)
    {
        builder.ToTable("WorkoutDays"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired(); builder.Property(x => x.NameAr).HasMaxLength(100);
        builder.Property(x => x.Subtitle).HasMaxLength(100); builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.WorkoutPlanId, x.Order }).IsUnique();
        builder.HasOne<WorkoutPlan>().WithMany().HasForeignKey(x => x.WorkoutPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutExerciseConfiguration : IEntityTypeConfiguration<WorkoutExercise>
{
    public void Configure(EntityTypeBuilder<WorkoutExercise> builder)
    {
        builder.ToTable("WorkoutExercises"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Sets).HasMaxLength(50); builder.Property(x => x.Repetitions).HasMaxLength(50);
        builder.Property(x => x.Rest).HasMaxLength(50); builder.Property(x => x.Tempo).HasMaxLength(50);
        builder.Property(x => x.RpeRir).HasMaxLength(50); builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.WorkoutDayId, x.Order }).IsUnique();
        builder.HasOne<WorkoutDay>().WithMany().HasForeignKey(x => x.WorkoutDayId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}
