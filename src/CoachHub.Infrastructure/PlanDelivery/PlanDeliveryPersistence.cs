using System.Text.Json;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.PlanDelivery;

public sealed class DeliveredPlanConfiguration : IEntityTypeConfiguration<DeliveredPlan>
{
    public void Configure(EntityTypeBuilder<DeliveredPlan> b)
    {
        b.ToTable("DeliveredPlans"); b.HasKey(x => x.Id); b.Property(x => x.PlanType).HasConversion<string>().HasMaxLength(20); b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PlanNameSnapshot).HasMaxLength(255); b.Property(x => x.Language).HasMaxLength(5); b.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)"); b.HasIndex(x => new { x.ClientId, x.DeliveredAt });
        b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class PlanDeliveryRepository(CoachHubDbContext db, IDietPlanRepository diets, IWorkoutPlanRepository workouts) : IPlanDeliveryRepository
{
    public async Task<(string Name, string SnapshotJson)?> SnapshotAsync(DeliveredPlanType type, Guid planId, Guid versionId, string language, CancellationToken token)
    {
        if (type == DeliveredPlanType.Diet)
        {
            var aggregate = await diets.FindAsync(planId, false, token);
            var version = aggregate?.Versions.SingleOrDefault(x => x.Id == versionId);
            if (aggregate is null || version is null) return null;
            var meals = aggregate.Meals.Where(x => x.DietPlanVersionId == versionId).ToArray();
            var mealIds = meals.Select(x => x.Id).ToHashSet();
            var groups = aggregate.ReplacementGroups.Where(x => x.DietPlanVersionId == versionId).ToArray();
            var groupIds = groups.Select(x => x.Id).ToHashSet();
            var snapshot = new
            {
                aggregate.Plan,
                Version = version,
                Notes = aggregate.Notes,
                Meals = meals,
                FoodItems = aggregate.FoodItems.Where(x => mealIds.Contains(x.MealId)),
                ReplacementGroups = groups,
                ReplacementOptions = aggregate.ReplacementOptions.Where(x => groupIds.Contains(x.DietReplacementGroupId))
            };
            var name = language == "ar" ? aggregate.Plan.NameAr ?? aggregate.Plan.NameEn : aggregate.Plan.NameEn;
            return (name, JsonSerializer.Serialize(snapshot));
        }

        if (versionId != planId) return null;
        var workout = await workouts.FindAsync(planId, false, token);
        if (workout is null) return null;
        var workoutName = language == "ar" ? workout.Plan.NameAr ?? workout.Plan.NameEn : workout.Plan.NameEn;
        return (workoutName, JsonSerializer.Serialize(workout));
    }
    public async Task<IReadOnlyList<DeliveredPlan>> ListAsync(Guid clientId, CancellationToken token) => await db.Set<DeliveredPlan>().AsNoTracking().Where(x => x.ClientId == clientId).OrderByDescending(x => x.DeliveredAt).ToArrayAsync(token);
    public async Task AddAsync(DeliveredPlan delivery, CancellationToken token) { db.Add(delivery); await db.SaveChangesAsync(token); }
}
