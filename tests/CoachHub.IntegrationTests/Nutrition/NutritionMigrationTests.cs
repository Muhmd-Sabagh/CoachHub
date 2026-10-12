using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoachHub.IntegrationTests.Nutrition;

/// <summary>
/// The NutritionEngine migration turns free-text food units into typed base units (D3), sets serving
/// sizes, and classifies existing meals by name.
/// </summary>
public sealed class NutritionMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261012213311_PlanVersioning";
    private readonly SqlServerTestDatabase _database = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Legacy_units_become_typed_and_meals_get_a_type()
    {
        Guid category = Guid.NewGuid(), grams = Guid.NewGuid(), milk = Guid.NewGuid(), toast = Guid.NewGuid(), odd = Guid.NewGuid();
        Guid coach = Guid.NewGuid(), plan = Guid.NewGuid(), revision = Guid.NewGuid(), variant = Guid.NewGuid();
        Guid breakfast = Guid.NewGuid(), dinner = Guid.NewGuid(), other = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO [FoodCategories] ([Id], [NameEn], [IsActive]) VALUES ({category}, N'Legacy', 1);
                INSERT INTO [FoodItems] ([Id], [NameEn], [FoodCategoryId], [MeasurementUnit], [CaloriesPer100], [ProteinPer100], [CarbohydratesPer100], [FatPer100], [IsActive])
                VALUES ({grams}, N'Rice', {category}, N'gram', 130, 2.7, 28, 0.3, 1),
                       ({milk}, N'Milk', {category}, N'مل', 55, 3.4, 4.8, 2, 1),
                       ({toast}, N'Toast', {category}, N'شريحة', 8000, 300, 1500, 100, 1),
                       ({odd}, N'Celery', {category}, N'handful', 0, 0, 0, 0, 1);
                INSERT INTO [Coaches] ([Id], [DisplayName], [IsActive], [CreatedAt]) VALUES ({coach}, N'Legacy coach', 1, SYSDATETIMEOFFSET());
                INSERT INTO [DietPlans] ([Id], [CoachId], [NameEn], [CreatedAt]) VALUES ({plan}, {coach}, N'Plan', SYSDATETIMEOFFSET());
                INSERT INTO [DietPlanRevisions] ([Id], [PlanId], [VersionNumber], [Status], [CreatedAt], [UpdatedAt])
                    VALUES ({revision}, {plan}, 1, N'Active', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                INSERT INTO [DietPlanVariants] ([Id], [RevisionId], [NameEn], [Order], [IsActiveForPdf]) VALUES ({variant}, {revision}, N'Day', 0, 1);
                INSERT INTO [DietPlanMeals] ([Id], [RevisionId], [DietPlanVariantId], [NameEn], [NameAr], [Order])
                VALUES ({breakfast}, {revision}, {variant}, N'Breakfast', NULL, 0),
                       ({dinner}, {revision}, {variant}, N'Meal 3', N'عشاء', 1),
                       ({other}, {revision}, {variant}, N'Meal 2', NULL, 2);
                """);
            await context.Database.MigrateAsync();
        }

        await using var migrated = _database.CreateContext();
        var foods = await migrated.Set<FoodItem>().ToDictionaryAsync(x => x.Id);
        Assert.Equal((FoodBaseUnit.Gram, 100m, (string?)null), (foods[grams].BaseUnit, foods[grams].ServingSize, foods[grams].PieceNameEn));
        Assert.Equal(FoodBaseUnit.Milliliter, foods[milk].BaseUnit);
        Assert.Equal((FoodBaseUnit.Piece, 1m, "slice", "شريحة"), (foods[toast].BaseUnit, foods[toast].ServingSize, foods[toast].PieceNameEn, foods[toast].PieceNameAr));
        Assert.Equal((FoodBaseUnit.Piece, "piece", (string?)null), (foods[odd].BaseUnit, foods[odd].PieceNameEn, foods[odd].PieceNameAr));
        Assert.All(foods.Values, food => Assert.Null(food.CoachId));

        var meals = await migrated.Set<Meal>().ToDictionaryAsync(x => x.Id);
        Assert.Equal((MealType.Breakfast, MealType.Dinner, MealType.Snack), (meals[breakfast].Type, meals[dinner].Type, meals[other].Type));
    }
}
