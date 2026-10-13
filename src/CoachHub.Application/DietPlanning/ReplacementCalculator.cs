using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.DietPlanning;

/// <summary>
/// Equivalent quantities for flexible alternatives (§7): how much of a replacement food matches the
/// original, by calories or by the original's dominant macro, per the coach's replacement rule (§49).
/// </summary>
public static class ReplacementCalculator
{
    /// <summary>The nutrient a replacement is matched on.</summary>
    public enum Basis { Calories, Protein, Carbohydrates, Fat }

    /// <summary>
    /// The macro that gives the original most of its calories (protein and carbohydrates 4 kcal/g, fat 9).
    /// Ties go to protein, then carbohydrates.
    /// </summary>
    public static Basis DominantMacro(NutritionTotals original)
    {
        var protein = original.Protein * 4m;
        var carbohydrates = original.Carbohydrates * 4m;
        var fat = original.Fat * 9m;
        if (protein >= carbohydrates && protein >= fat) return Basis.Protein;
        return carbohydrates >= fat ? Basis.Carbohydrates : Basis.Fat;
    }

    /// <summary>
    /// The quantity of <paramref name="replacement"/>, in <paramref name="unitId"/> (or its base unit), that
    /// matches <paramref name="original"/>. With <see cref="FoodReplacementRule.DominantMacro"/> it matches the
    /// original's dominant macro, falling back to calories when the replacement has none of it. Null when
    /// nothing can be matched (the original or the replacement has no calories).
    /// Grams and millilitres round to whole numbers; pieces and named units to the nearest half.
    /// </summary>
    public static decimal? EquivalentQuantity(
        NutritionTotals original, FoodItem replacement, Guid? unitId, FoodReplacementRule rule)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);
        var unit = replacement.FindUnit(unitId);
        if (unitId is not null && unit is null) throw new ArgumentException("The unit does not belong to the food.", nameof(unitId));

        var baseAmount = rule == FoodReplacementRule.DominantMacro
            ? Match(original, replacement, DominantMacro(original)) ?? Match(original, replacement, Basis.Calories)
            : Match(original, replacement, Basis.Calories);
        if (baseAmount is not { } amount) return null;

        var quantity = unit is null ? amount : amount / unit.BaseQuantity;
        var counted = unit is not null || replacement.BaseUnit == FoodBaseUnit.Piece;
        var rounded = counted
            ? Math.Max(0.5m, decimal.Round(quantity * 2m, MidpointRounding.AwayFromZero) / 2m)
            : Math.Max(1m, decimal.Round(quantity, MidpointRounding.AwayFromZero));
        return Math.Min(rounded, 10000m);
    }

    /// <summary>The base-unit amount of the replacement holding as much of the nutrient as the original.</summary>
    private static decimal? Match(NutritionTotals original, FoodItem replacement, Basis basis)
    {
        var (target, per100) = basis switch
        {
            Basis.Protein => (original.Protein, replacement.ProteinPer100),
            Basis.Carbohydrates => (original.Carbohydrates, replacement.CarbohydratesPer100),
            Basis.Fat => (original.Fat, replacement.FatPer100),
            _ => (original.Calories, replacement.CaloriesPer100)
        };
        return target > 0 && per100 > 0 ? target / (per100 / 100m) : null;
    }
}
