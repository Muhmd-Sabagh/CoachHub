using CoachHub.Application.DietPlanning;

namespace CoachHub.Application.Tests.DietPlanning;

public sealed class NutritionCalculatorTests
{
    [Fact]
    public void Energy_calculator_preserves_legacy_mifflin_st_jeor_and_goal_adjustment()
    {
        var result = NutritionCalculator.CalculateEnergy(new(
            30, BiologicalSex.Male, 80, 180, 1.55m, EnergyGoal.LoseWeight));

        Assert.Equal(1780m, result.BasalMetabolicRate);
        Assert.Equal(2759m, result.MaintenanceCalories);
        Assert.Equal(2259m, result.GoalCalories);
        Assert.Equal(160m, result.Protein);
        Assert.Equal(263.56m, result.Carbohydrates);
        Assert.Equal(62.75m, result.Fat);
    }
}
