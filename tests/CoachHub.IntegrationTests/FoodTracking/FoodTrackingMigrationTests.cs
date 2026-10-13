using CoachHub.Domain.DietPlanning;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoachHub.IntegrationTests.FoodTracking;

/// <summary>The FoodTracking migration keeps existing alternatives as Exact: their quantities were set by the coach.</summary>
public sealed class FoodTrackingMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261012224236_NutritionEngine";
    private readonly SqlServerTestDatabase _database = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Existing_alternatives_become_exact()
    {
        Guid category = Guid.NewGuid(), rice = Guid.NewGuid(), potato = Guid.NewGuid(), coach = Guid.NewGuid(), plan = Guid.NewGuid();
        Guid revision = Guid.NewGuid(), variant = Guid.NewGuid(), meal = Guid.NewGuid(), row = Guid.NewGuid();
        Guid group = Guid.NewGuid(), option = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO [FoodCategories] ([Id], [NameEn], [IsActive]) VALUES ({category}, N'Staples', 1);
                INSERT INTO [FoodItems] ([Id], [NameEn], [FoodCategoryId], [BaseUnit], [ServingSize], [CaloriesPer100], [ProteinPer100], [CarbohydratesPer100], [FatPer100], [IsActive])
                VALUES ({rice}, N'Rice', {category}, N'Gram', 100, 130, 2.7, 28, 0.3, 1),
                       ({potato}, N'Potato', {category}, N'Gram', 100, 77, 2, 17, 0.1, 1);
                INSERT INTO [Coaches] ([Id], [DisplayName], [IsActive], [CreatedAt]) VALUES ({coach}, N'Coach', 1, SYSDATETIMEOFFSET());
                INSERT INTO [DietPlans] ([Id], [CoachId], [NameEn], [CreatedAt]) VALUES ({plan}, {coach}, N'Plan', SYSDATETIMEOFFSET());
                INSERT INTO [DietPlanRevisions] ([Id], [PlanId], [VersionNumber], [Status], [CreatedAt], [UpdatedAt])
                    VALUES ({revision}, {plan}, 1, N'Active', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                INSERT INTO [DietPlanVariants] ([Id], [RevisionId], [NameEn], [Order], [IsActiveForPdf]) VALUES ({variant}, {revision}, N'Day', 0, 1);
                INSERT INTO [DietPlanMeals] ([Id], [RevisionId], [DietPlanVariantId], [NameEn], [Type], [Order])
                    VALUES ({meal}, {revision}, {variant}, N'Lunch', N'Lunch', 0);
                INSERT INTO [DietPlanMealFoods] ([Id], [RevisionId], [MealId], [FoodItemId], [Quantity], [Order])
                    VALUES ({row}, {revision}, {meal}, {rice}, 200, 0);
                INSERT INTO [DietReplacementGroups] ([Id], [RevisionId], [DietPlanVariantId], [TargetMealId], [TargetMealFoodItemId], [Title], [Order])
                    VALUES ({group}, {revision}, {variant}, {meal}, {row}, N'Carbs', 0);
                INSERT INTO [DietReplacementOptions] ([Id], [RevisionId], [DietReplacementGroupId], [ReplacementFoodItemId], [Quantity], [Order])
                    VALUES ({option}, {revision}, {group}, {potato}, 300, 0);
                """);
            await context.Database.MigrateAsync();
        }

        await using var migrated = _database.CreateContext();
        Assert.Equal(ReplacementMode.Exact, (await migrated.Set<DietReplacementGroup>().SingleAsync(x => x.Id == group)).Mode);
        var kept = await migrated.Set<DietReplacementOption>().SingleAsync(x => x.Id == option);
        Assert.Equal((300m, (Guid?)null), (kept.Quantity!.Value, kept.FoodUnitId));
        Assert.Null((await migrated.Set<MealFoodItem>().SingleAsync(x => x.Id == row)).MealOptionId);
    }
}
