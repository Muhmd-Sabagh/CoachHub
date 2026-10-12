using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.DietPlanning;

/// <summary>Nutrients of a food quantity (§6): per-100 values scaled by the base-unit amount.</summary>
public static class NutritionCalculator
{
    /// <summary>
    /// The nutrients of <paramref name="quantity"/> of a food, in one of its named units or its base unit.
    /// <see cref="NutritionTotals.Weight"/> is the base-unit amount. Unrecorded fiber counts as zero.
    /// </summary>
    public static NutritionTotals Calculate(FoodItem food, decimal quantity, Guid? unitId = null)
    {
        var amount = food.BaseAmount(quantity, unitId);
        var factor = amount / 100m;
        return Round(new(
            amount,
            food.CaloriesPer100 * factor,
            food.ProteinPer100 * factor,
            food.CarbohydratesPer100 * factor,
            food.FatPer100 * factor,
            (food.FiberPer100 ?? 0m) * factor));
    }

    public static NutritionTotals Round(NutritionTotals value) => new(
        decimal.Round(value.Weight, 2), decimal.Round(value.Calories, 2),
        decimal.Round(value.Protein, 2), decimal.Round(value.Carbohydrates, 2),
        decimal.Round(value.Fat, 2), decimal.Round(value.Fiber, 2));
}

/// <summary>The body and goal an energy calculation is for.</summary>
public sealed record EnergyProfile(int Age, BiologicalSex Sex, decimal WeightKg, decimal HeightCm, ActivityLevel ActivityLevel, EnergyGoal Goal);

/// <summary>Macronutrient grams for a day.</summary>
public sealed record MacroGrams(decimal Protein, decimal Carbohydrates, decimal Fat);

/// <summary>One way of turning a calorie target into macros (§15).</summary>
public interface IMacroStrategy
{
    MacroMode Mode { get; }
    MacroGrams Calculate(decimal calories, decimal weightKg, MacroPreferences parameters);
}

/// <summary>Mode 1: protein and fat in g/kg of body weight; carbohydrates fill the remaining calories.</summary>
public sealed class GramsPerKgMacroStrategy : IMacroStrategy
{
    public MacroMode Mode => MacroMode.GramsPerKg;

    public MacroGrams Calculate(decimal calories, decimal weightKg, MacroPreferences parameters) =>
        MacroStrategies.WithCarbohydrateRemainder(calories,
            weightKg * (parameters.ProteinGramsPerKg ?? 0m), weightKg * (parameters.FatGramsPerKg ?? 0m));
}

/// <summary>Mode 2: protein, carbohydrate and fat as percentages of the calorie target.</summary>
public sealed class PercentagesMacroStrategy : IMacroStrategy
{
    public MacroMode Mode => MacroMode.Percentages;

    public MacroGrams Calculate(decimal calories, decimal weightKg, MacroPreferences parameters) => new(
        calories * (parameters.ProteinPercent ?? 0m) / 100m / EnergyCalculator.ProteinCaloriesPerGram,
        calories * (parameters.CarbohydratePercent ?? 0m) / 100m / EnergyCalculator.CarbohydrateCaloriesPerGram,
        calories * (parameters.FatPercent ?? 0m) / 100m / EnergyCalculator.FatCaloriesPerGram);
}

/// <summary>Mode 3: fixed protein and fat grams; carbohydrates fill the remaining calories.</summary>
public sealed class FixedGramsMacroStrategy : IMacroStrategy
{
    public MacroMode Mode => MacroMode.FixedGrams;

    public MacroGrams Calculate(decimal calories, decimal weightKg, MacroPreferences parameters) =>
        MacroStrategies.WithCarbohydrateRemainder(calories, parameters.ProteinGrams ?? 0m, parameters.FatGrams ?? 0m);
}

public static class MacroStrategies
{
    private static readonly IReadOnlyDictionary<MacroMode, IMacroStrategy> All = new IMacroStrategy[]
    {
        new GramsPerKgMacroStrategy(), new PercentagesMacroStrategy(), new FixedGramsMacroStrategy()
    }.ToDictionary(strategy => strategy.Mode);

    public static IMacroStrategy For(MacroMode mode) =>
        All.TryGetValue(mode, out var strategy) ? strategy : throw new ArgumentOutOfRangeException(nameof(mode));

    /// <summary>Carbohydrates take whatever calories protein and fat leave, never below zero.</summary>
    internal static MacroGrams WithCarbohydrateRemainder(decimal calories, decimal protein, decimal fat) => new(
        protein,
        decimal.Max(0m, (calories - protein * EnergyCalculator.ProteinCaloriesPerGram - fat * EnergyCalculator.FatCaloriesPerGram)
            / EnergyCalculator.CarbohydrateCaloriesPerGram),
        fat);
}

/// <summary>
/// The calorie and macro engine (§14, §15). Mifflin-St Jeor BMR; TDEE = BMR × the coach's factor for the
/// activity level; goal calories adjust TDEE by the coach's deficit or surplus percentage. Coefficients
/// always come from the coach's settings (§49), never from constants here.
/// </summary>
public static class EnergyCalculator
{
    public const decimal ProteinCaloriesPerGram = 4m;
    public const decimal CarbohydrateCaloriesPerGram = 4m;
    public const decimal FatCaloriesPerGram = 9m;

    public static decimal BasalMetabolicRate(BiologicalSex sex, decimal weightKg, decimal heightCm, int age) =>
        10m * weightKg + 6.25m * heightCm - 5m * age + (sex == BiologicalSex.Male ? 5m : -161m);

    /// <summary>The deficit (negative) or surplus (positive) percentage applied for the goal.</summary>
    public static decimal AdjustmentPercent(EnergyGoal goal, CoachSettings settings) => goal switch
    {
        EnergyGoal.LoseWeight => -settings.DeficitPercent,
        EnergyGoal.GainWeight => settings.SurplusPercent,
        _ => 0m
    };

    /// <summary>
    /// The client's macro settings with the coach's defaults filled in. Percentages and fixed grams only
    /// exist per client, so a client without settings uses mode 1 with the coach's g/kg defaults (the
    /// coach's default mode preselects the mode when the coach edits a client).
    /// </summary>
    public static MacroPreferences Resolve(MacroPreferences? client, CoachSettings settings)
    {
        if (client is null || client.Mode == MacroMode.GramsPerKg)
        {
            return new MacroPreferences(MacroMode.GramsPerKg,
                client?.ProteinGramsPerKg ?? settings.ProteinGramsPerKg,
                client?.FatGramsPerKg ?? settings.FatGramsPerKg);
        }
        return client.Normalize();
    }

    public static EnergyCalculatorResponse Calculate(EnergyProfile profile, MacroPreferences? macros, CoachSettings settings)
    {
        var factor = settings.FactorFor(profile.ActivityLevel);
        var bmr = BasalMetabolicRate(profile.Sex, profile.WeightKg, profile.HeightCm, profile.Age);
        var maintenance = bmr * factor;
        var adjustment = AdjustmentPercent(profile.Goal, settings);
        var goalCalories = decimal.Max(0m, maintenance * (1m + adjustment / 100m));
        var parameters = Resolve(macros, settings);
        var grams = MacroStrategies.For(parameters.Mode).Calculate(goalCalories, profile.WeightKg, parameters);
        return new(
            decimal.Round(profile.WeightKg, 2), profile.Age, profile.ActivityLevel, factor, profile.Goal, adjustment,
            decimal.Round(bmr, 2), decimal.Round(maintenance, 2), decimal.Round(goalCalories, 2),
            parameters,
            decimal.Round(grams.Protein, 2), decimal.Round(grams.Carbohydrates, 2), decimal.Round(grams.Fat, 2));
    }
}
