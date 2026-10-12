using CoachHub.Application.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Tests.DietPlanning;

public sealed class NutritionCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly EnergyProfile Man = new(30, BiologicalSex.Male, 80, 180, ActivityLevel.ModeratelyActive, EnergyGoal.LoseWeight);

    private static CoachSettings Defaults() => CoachSettings.CreateDefault(Guid.NewGuid(), Now);

    [Fact]
    public void Mifflin_st_jeor_bmr_for_both_sexes()
    {
        Assert.Equal(1780m, EnergyCalculator.BasalMetabolicRate(BiologicalSex.Male, 80, 180, 30));
        Assert.Equal(1315.25m, EnergyCalculator.BasalMetabolicRate(BiologicalSex.Female, 62, 165, 35));
    }

    [Fact]
    public void Goal_calories_use_the_coach_deficit_and_mode_one_defaults()
    {
        var result = EnergyCalculator.Calculate(Man, null, Defaults());

        Assert.Equal(1780m, result.BasalMetabolicRate);
        Assert.Equal((1.55m, 2759m), (result.ActivityFactor, result.MaintenanceCalories));
        Assert.Equal((-20m, 2207.2m), (result.AdjustmentPercent, result.GoalCalories));
        Assert.Equal(MacroMode.GramsPerKg, result.Macros.Mode);
        Assert.Equal((160m, 64m), (result.Protein, result.Fat));
        Assert.Equal(247.8m, result.Carbohydrates);
    }

    [Fact]
    public void Surplus_and_maintenance_follow_the_goal()
    {
        Assert.Equal(3034.9m, EnergyCalculator.Calculate(Man with { Goal = EnergyGoal.GainWeight }, null, Defaults()).GoalCalories);
        Assert.Equal(2759m, EnergyCalculator.Calculate(Man with { Goal = EnergyGoal.Maintain }, null, Defaults()).GoalCalories);
    }

    [Fact]
    public void Coefficients_come_from_the_coach_settings()
    {
        var settings = Defaults();
        settings.Update(new ActivityFactors(1.2m, 1.4m, 1.6m, 1.8m, 2.0m), 25m, 15m, 2.2m, 1.0m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5), FoodReplacementRule.Calories, Now);

        var result = EnergyCalculator.Calculate(Man, null, settings);

        Assert.Equal((2848m, 2136m), (result.MaintenanceCalories, result.GoalCalories));
        Assert.Equal((176m, 80m), (result.Protein, result.Fat));
    }

    [Fact]
    public void Mode_one_client_values_override_coach_defaults_one_by_one()
    {
        var result = EnergyCalculator.Calculate(Man, new MacroPreferences(MacroMode.GramsPerKg, ProteinGramsPerKg: 2.5m), Defaults());
        Assert.Equal((200m, 64m), (result.Protein, result.Fat));
    }

    [Fact]
    public void Mode_two_splits_calories_by_percentage()
    {
        var grams = MacroStrategies.For(MacroMode.Percentages).Calculate(2000m, 80m,
            new MacroPreferences(MacroMode.Percentages, ProteinPercent: 30, CarbohydratePercent: 40, FatPercent: 30));
        Assert.Equal((150m, 200m), (grams.Protein, grams.Carbohydrates));
        Assert.Equal(66.67m, decimal.Round(grams.Fat, 2));
    }

    [Fact]
    public void Mode_three_fixes_protein_and_fat_and_carbohydrates_fill_the_rest()
    {
        var grams = MacroStrategies.For(MacroMode.FixedGrams).Calculate(2000m, 80m,
            new MacroPreferences(MacroMode.FixedGrams, ProteinGrams: 150, FatGrams: 60));
        Assert.Equal(new MacroGrams(150m, 215m, 60m), grams);

        var tooMuch = MacroStrategies.For(MacroMode.FixedGrams).Calculate(1000m, 80m,
            new MacroPreferences(MacroMode.FixedGrams, ProteinGrams: 200, FatGrams: 80));
        Assert.Equal(0m, tooMuch.Carbohydrates);
    }

    [Fact]
    public void Food_rows_scale_by_unit_and_include_fiber()
    {
        var oats = FoodItem.Create(null, new FoodValues("Oats", null, Guid.NewGuid(), FoodBaseUnit.Gram, 80, 389, 16.9m, 66.3m, 6.9m,
            FiberPer100: 10.6m), [new FoodUnitValues(null, "cup", null, 80)]);
        var cup = oats.Units.Single();

        var totals = NutritionCalculator.Calculate(oats, 1.5m, cup.Id);

        Assert.Equal(new NutritionTotals(120m, 466.8m, 20.28m, 79.56m, 8.28m, 12.72m), totals);
        Assert.Equal(NutritionCalculator.Calculate(oats, 120m), totals);

        var eggs = FoodItem.Create(null, new FoodValues("Egg", null, Guid.NewGuid(), FoodBaseUnit.Piece, 1, 7150, 600, 50, 500,
            PieceNameEn: "large"));
        Assert.Equal(214.5m, NutritionCalculator.Calculate(eggs, 3).Calories);
        Assert.Equal(0m, NutritionCalculator.Calculate(eggs, 3).Fiber);
    }
}
