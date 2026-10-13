using CoachHub.Application.FoodTracking;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Nutrition;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.FoodTracking;

public sealed class FoodLogEntryConfiguration : IEntityTypeConfiguration<FoodLogEntry>
{
    public void Configure(EntityTypeBuilder<FoodLogEntry> builder)
    {
        builder.ToTable("FoodLogEntries", table =>
        {
            table.HasCheckConstraint("CK_FoodLogEntries_Portion",
                "([FoodItemId] IS NULL AND [Quantity] IS NULL AND [BaseAmount] IS NULL AND [FoodUnitId] IS NULL) OR ([FoodItemId] IS NOT NULL AND [Quantity] > 0 AND [BaseAmount] > 0 AND [UnitLabel] IS NOT NULL)");
            table.HasCheckConstraint("CK_FoodLogEntries_Nutrients",
                "[Calories] >= 0 AND [Protein] >= 0 AND [Carbohydrates] >= 0 AND [Fat] >= 0 AND [Fiber] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slot).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(FoodLogEntry.MaximumNameLength).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(FoodLogEntry.MaximumNameLength);
        builder.Property(x => x.UnitLabel).HasMaxLength(50);
        builder.Property(x => x.UnitLabelAr).HasMaxLength(50);
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
        builder.Property(x => x.BaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.Calories).HasPrecision(8, 2);
        builder.Property(x => x.Protein).HasPrecision(7, 2);
        builder.Property(x => x.Carbohydrates).HasPrecision(7, 2);
        builder.Property(x => x.Fat).HasPrecision(7, 2);
        builder.Property(x => x.Fiber).HasPrecision(7, 2);
        builder.Ignore(x => x.Nutrients);
        builder.Ignore(x => x.IsCustom);
        builder.HasIndex(x => new { x.ClientId, x.Date });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // A food in a diary cannot be deleted (deactivate it instead), like a food in a plan.
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.FoodItemId).OnDelete(DeleteBehavior.Restrict);
        // A removed unit leaves the entry as it was logged: its quantity, label and nutrients are kept.
        builder.HasOne<FoodUnit>().WithMany().HasForeignKey(x => x.FoodUnitId).OnDelete(DeleteBehavior.SetNull);
        // PlannedMealId, PlannedMealFoodItemId and ReplacementOptionId point at plan version rows, which are deleted
        // with their plan. They are references only (no foreign keys); the entry stands on its own.
    }
}

public sealed class NutritionDayConfiguration : IEntityTypeConfiguration<NutritionDay>
{
    public void Configure(EntityTypeBuilder<NutritionDay> builder)
    {
        builder.ToTable("NutritionDays");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ClientId, x.Date }).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MealChoices).WithOne().HasForeignKey(x => x.NutritionDayId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.MealChoices).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class NutritionDayMealChoiceConfiguration : IEntityTypeConfiguration<NutritionDayMealChoice>
{
    public void Configure(EntityTypeBuilder<NutritionDayMealChoice> builder)
    {
        builder.ToTable("NutritionDayMealChoices");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.NutritionDayId, x.MealId }).IsUnique();
    }
}

public sealed class FoodTrackingRepository(CoachHubDbContext dbContext) : IFoodTrackingRepository
{
    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DietVersion>> ListDietVersionsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var rows = await (
            from plan in dbContext.Set<DietPlan>().AsNoTracking()
            join revision in dbContext.Set<DietPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where plan.ClientId == clientId
            select new { plan, revision }).ToListAsync(cancellationToken);
        return rows.Select(x => new DietVersion(x.plan, x.revision)).ToArray();
    }

    public Task<NutritionDay?> FindDayAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.Set<NutritionDay>().SingleOrDefaultAsync(x => x.ClientId == clientId && x.Date == date, cancellationToken);

    public async Task<IReadOnlyList<NutritionDay>> ListDaysAsync(
        Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.Set<NutritionDay>().AsNoTracking()
            .Where(x => x.ClientId == clientId && x.Date >= from && x.Date <= to).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FoodLogEntry>> ListEntriesAsync(
        Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.Set<FoodLogEntry>().AsNoTracking()
            .Where(x => x.ClientId == clientId && x.Date >= from && x.Date <= to).ToListAsync(cancellationToken);

    public Task<FoodLogEntry?> FindEntryAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<FoodLogEntry>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(NutritionDay day) => dbContext.Add(day);
    public void Add(IEnumerable<FoodLogEntry> entries) => dbContext.AddRange(entries);
    public void Remove(FoodLogEntry entry) => dbContext.Remove(entry);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
