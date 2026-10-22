using System.Text.Json;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Domain.PlanDelivery;
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
    public async Task<PlanSnapshot?> SnapshotAsync(
        DeliveredPlanType type, Guid planId, int? versionNumber, Guid? variantId, string language, CancellationToken token)
    {
        var arabic = language.Trim().Equals("ar", StringComparison.OrdinalIgnoreCase);
        if (type == DeliveredPlanType.Diet)
        {
            var aggregate = await diets.FindAsync(planId, versionNumber, false, token);
            var variant = aggregate?.Variants.SingleOrDefault(x => x.Id == variantId);
            if (aggregate is null || variant is null) return null;
            var meals = aggregate.Meals.Where(x => x.DietPlanVariantId == variant.Id).ToArray();
            var mealIds = meals.Select(x => x.Id).ToHashSet();
            var groups = aggregate.ReplacementGroups.Where(x => x.DietPlanVariantId == variant.Id).ToArray();
            var groupIds = groups.Select(x => x.Id).ToHashSet();
            var snapshot = new
            {
                aggregate.Plan,
                aggregate.Revision,
                Variant = variant,
                Notes = aggregate.Notes,
                Meals = meals,
                MealOptions = aggregate.MealOptions.Where(x => mealIds.Contains(x.MealId)),
                FoodItems = aggregate.FoodItems.Where(x => mealIds.Contains(x.MealId)),
                ReplacementGroups = groups,
                ReplacementOptions = aggregate.ReplacementOptions.Where(x => groupIds.Contains(x.DietReplacementGroupId))
            };
            var name = arabic ? aggregate.Plan.NameAr ?? aggregate.Plan.NameEn : aggregate.Plan.NameEn;
            return new(aggregate.Plan.CoachId, name, aggregate.Revision.Id, aggregate.Revision.VersionNumber, JsonSerializer.Serialize(snapshot));
        }

        if (variantId is not null) return null;
        var workout = await workouts.FindAsync(planId, versionNumber, false, token);
        if (workout is null) return null;
        var workoutName = arabic ? workout.Plan.NameAr ?? workout.Plan.NameEn : workout.Plan.NameEn;
        var workoutSnapshot = new { workout.Plan, workout.Revision, workout.Notes, workout.Days, workout.Exercises, workout.SetGroups, workout.Sets };
        return new(workout.Plan.CoachId, workoutName, workout.Revision.Id, workout.Revision.VersionNumber, JsonSerializer.Serialize(workoutSnapshot));
    }
    public async Task<IReadOnlyList<DeliveredPlan>> ListAsync(Guid clientId, CancellationToken token) => await db.Set<DeliveredPlan>().AsNoTracking().Where(x => x.ClientId == clientId).OrderByDescending(x => x.DeliveredAt).ToArrayAsync(token);
    public async Task AddAsync(DeliveredPlan delivery, CancellationToken token) { db.Add(delivery); await db.SaveChangesAsync(token); }
}
