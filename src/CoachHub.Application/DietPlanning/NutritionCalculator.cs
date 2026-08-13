using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.DietPlanning;

public static class NutritionCalculator
{
    public static NutritionTotals Calculate(FoodItem food, decimal quantity)
    {
        var factor = quantity / 100m;
        return Round(new(
            quantity,
            food.CaloriesPer100 * factor,
            food.ProteinPer100 * factor,
            food.CarbohydratesPer100 * factor,
            food.FatPer100 * factor));
    }

    public static NutritionTotals Round(NutritionTotals value) => new(
        decimal.Round(value.Weight, 2), decimal.Round(value.Calories, 2),
        decimal.Round(value.Protein, 2), decimal.Round(value.Carbohydrates, 2),
        decimal.Round(value.Fat, 2));

    public static EnergyCalculatorResponse CalculateEnergy(EnergyCalculatorInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Age is < 13 or > 120) errors["age"] = ["Age must be between 13 and 120."];
        if (input.WeightKg is <= 0 or > 500) errors["weightKg"] = ["Weight must be between 0 and 500 kg."];
        if (input.HeightCm is < 50 or > 300) errors["heightCm"] = ["Height must be between 50 and 300 cm."];
        if (input.ActivityFactor is < 1m or > 3m) errors["activityFactor"] = ["Activity factor must be between 1 and 3."];
        if (input.ProteinGramsPerKg is < 0m or > 5m) errors["proteinGramsPerKg"] = ["Protein must be between 0 and 5 g/kg."];
        if (input.FatCaloriesPercent is < 0m or > 1m) errors["fatCaloriesPercent"] = ["Fat percentage must be between 0 and 1."];
        if (errors.Count > 0) throw new ValidationException(errors);

        var sexAdjustment = input.Sex == BiologicalSex.Male ? 5m : -161m;
        var bmr = 10m * input.WeightKg + 6.25m * input.HeightCm - 5m * input.Age + sexAdjustment;
        var maintenance = bmr * input.ActivityFactor;
        var goalCalories = input.Goal switch
        {
            EnergyGoal.LoseWeight => maintenance - 500m,
            EnergyGoal.GainWeight => maintenance + 500m,
            _ => maintenance
        };
        goalCalories = decimal.Max(0, goalCalories);
        var protein = input.WeightKg * input.ProteinGramsPerKg;
        var fat = goalCalories * input.FatCaloriesPercent / 9m;
        var carbohydrates = decimal.Max(0, (goalCalories - protein * 4m - fat * 9m) / 4m);
        return new(
            decimal.Round(input.WeightKg, 2), decimal.Round(bmr, 2),
            decimal.Round(maintenance, 2), decimal.Round(goalCalories, 2),
            decimal.Round(protein, 2), decimal.Round(carbohydrates, 2), decimal.Round(fat, 2));
    }
}
