using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Tests.Nutrition;

public sealed class FoodItemTests
{
    private static FoodValues Values(
        FoodBaseUnit unit = FoodBaseUnit.Gram, string? pieceEn = null, decimal calories = 165, decimal serving = 100) =>
        new("  Chicken Breast  ", null, Guid.NewGuid(), unit, serving, calories, 31, 0, 3.6m,
            FiberPer100: 0, SugarPer100: 0, SodiumMgPer100: 74, PieceNameEn: pieceEn);

    [Fact]
    public void Create_normalizes_names_and_records_serving_and_micronutrients()
    {
        var food = FoodItem.Create(null, Values());

        Assert.Equal("Chicken Breast", food.NameEn);
        Assert.Null(food.NameAr);
        Assert.True(food.IsPlatform);
        Assert.Equal(("g", "غ"), (food.BaseUnitLabel, food.BaseUnitLabelAr));
        Assert.Equal(100m, food.ServingSize);
        Assert.Equal(74m, food.SodiumMgPer100);
        Assert.Null(food.PieceNameEn);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(15001, 0, 0, 0)]
    public void Create_rejects_macros_outside_supported_ranges(
        decimal calories, decimal protein, decimal carbohydrates, decimal fat) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodItem.Create(null,
            new FoodValues("Invalid", null, Guid.NewGuid(), FoodBaseUnit.Gram, 100, calories, protein, carbohydrates, fat)));

    [Fact]
    public void Optional_micronutrients_and_serving_are_range_checked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodItem.Create(null, Values() with { FiberPer100 = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodItem.Create(null, Values() with { SodiumMgPer100 = 2_000_000 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodItem.Create(null, Values(serving: 0)));
        Assert.Null(FoodItem.Create(null, Values() with { FiberPer100 = null }).FiberPer100);
    }

    [Fact]
    public void Piece_foods_need_a_piece_name_and_other_units_drop_it()
    {
        Assert.Throws<ArgumentException>(() => FoodItem.Create(null, Values(FoodBaseUnit.Piece)));
        var egg = FoodItem.Create(null, Values(FoodBaseUnit.Piece, " large egg ") with { PieceNameAr = "بيضة" });
        Assert.Equal(("large egg", "بيضة"), (egg.BaseUnitLabel, egg.BaseUnitLabelAr));

        egg.Update(Values(FoodBaseUnit.Milliliter, "ignored"), []);
        Assert.Null(egg.PieceNameEn);
        Assert.Equal("ml", egg.BaseUnitLabel);
    }

    [Fact]
    public void Named_units_convert_to_base_units_and_sync_by_id()
    {
        var coach = Guid.NewGuid();
        var oats = FoodItem.Create(coach, Values(), [new(null, "cup", "كوب", 80), new(null, "scoop", null, 40)]);
        Assert.False(oats.IsPlatform);
        var cup = oats.Units.Single(x => x.NameEn == "cup");
        Assert.Equal(160m, oats.BaseAmount(2, cup.Id));
        Assert.Equal(50m, oats.BaseAmount(50, null));
        Assert.Throws<ArgumentException>(() => oats.BaseAmount(1, Guid.NewGuid()));

        oats.Update(Values(), [new(cup.Id, "cup", "كوب", 90), new(null, "bowl", null, 60)]);
        Assert.Equal(["cup", "bowl"], oats.Units.OrderBy(x => x.Order).Select(x => x.NameEn));
        Assert.Same(cup, oats.FindUnit(cup.Id));
        Assert.Equal(90m, cup.BaseQuantity);

        Assert.Throws<ArgumentException>(() => oats.Update(Values(), [new(null, "Cup", null, 1), new(null, "cup", null, 2)]));
        Assert.Throws<ArgumentException>(() => oats.Update(Values(), [new(Guid.NewGuid(), "spoon", null, 5)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => oats.Update(Values(), [new(null, "tub", null, 0)]));
    }

    [Theory]
    [InlineData("gram", FoodBaseUnit.Gram, null, null)]
    [InlineData(" g ", FoodBaseUnit.Gram, null, null)]
    [InlineData("مل", FoodBaseUnit.Milliliter, null, null)]
    [InlineData("شريحة", FoodBaseUnit.Piece, "slice", "شريحة")]
    [InlineData("سكوب", FoodBaseUnit.Piece, "scoop", "سكوب")]
    [InlineData("piece", FoodBaseUnit.Piece, "piece", null)]
    [InlineData("handful", FoodBaseUnit.Piece, "piece", null)]
    public void Legacy_units_map_to_typed_base_units(string legacy, FoodBaseUnit unit, string? pieceEn, string? pieceAr) =>
        Assert.Equal((unit, pieceEn, pieceAr), LegacyFoodUnits.Map(legacy));
}

public sealed class NutritionTypesTests
{
    [Fact]
    public void Macro_preferences_keep_only_the_chosen_mode_parameters()
    {
        var percentages = new MacroPreferences(MacroMode.Percentages, ProteinGramsPerKg: 2,
            ProteinPercent: 30, CarbohydratePercent: 45, FatPercent: 25).Normalize();
        Assert.Null(percentages.ProteinGramsPerKg);
        Assert.Equal(45m, percentages.CarbohydratePercent);

        var gramsPerKg = new MacroPreferences(MacroMode.GramsPerKg, FatGrams: 70).Normalize();
        Assert.Equal(new MacroPreferences(MacroMode.GramsPerKg), gramsPerKg);
    }

    [Fact]
    public void Macro_preferences_validate_each_mode()
    {
        Assert.Throws<ArgumentException>(() => new MacroPreferences(MacroMode.Percentages, ProteinPercent: 30, CarbohydratePercent: 40, FatPercent: 20).Normalize());
        Assert.Throws<ArgumentException>(() => new MacroPreferences(MacroMode.Percentages, ProteinPercent: 30).Normalize());
        Assert.Throws<ArgumentException>(() => new MacroPreferences(MacroMode.FixedGrams, ProteinGrams: 150).Normalize());
        Assert.Throws<ArgumentException>(() => new MacroPreferences(MacroMode.GramsPerKg, ProteinGramsPerKg: 6).Normalize());
        Assert.Throws<ArgumentException>(() => new MacroPreferences((MacroMode)9).Normalize());
        Assert.Equal(150m, new MacroPreferences(MacroMode.FixedGrams, ProteinGrams: 150, FatGrams: 60).Normalize().ProteinGrams);
    }

    [Theory]
    [InlineData("Breakfast", null, MealType.Breakfast)]
    [InlineData("Post-workout lunch", null, MealType.Lunch)]
    [InlineData("Meal", "عشاء", MealType.Dinner)]
    [InlineData("Meal 2", "وجبة", MealType.Snack)]
    public void Meal_types_are_guessed_from_names(string nameEn, string? nameAr, MealType expected) =>
        Assert.Equal(expected, MealTypes.FromName(nameEn, nameAr));

    [Fact]
    public void Diet_targets_are_range_checked_and_frozen_with_archived_versions()
    {
        Assert.Throws<ArgumentException>(() => new DietTargets(Calories: -1).Validate());
        Assert.Throws<ArgumentException>(() => new DietTargets(SnackCount: 16).Validate());

        var version = DietPlanRevision.Create(Guid.NewGuid(), 1, CoachHub.Domain.Common.PlanRevisionStatus.Active, null, null, null, DateTimeOffset.UtcNow);
        version.SetTargets(new DietTargets(2200, 160, 220, 70, 3, 2));
        Assert.Equal(new DietTargets(2200, 160, 220, 70, 3, 2), version.Targets);
        version.Archive(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => version.SetTargets(DietTargets.None));
    }
}
