using System.Text.Json.Serialization;

namespace CoachHub.Domain.DietPlanning;

/// <summary>The kind of a meal (§4, §8): what the client logs it as. The meal keeps its own name too.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MealType>))]
public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack
}

public static class MealTypes
{
    /// <summary>
    /// A best guess from a meal's name (English or Arabic), used when none is given and for meals created
    /// before meal types existed; anything unrecognised is a snack.
    /// </summary>
    public static MealType FromName(string? nameEn, string? nameAr = null)
    {
        var name = $"{nameEn} {nameAr}".ToLowerInvariant();
        if (name.Contains("breakfast") || name.Contains("فطور") || name.Contains("إفطار") || name.Contains("افطار"))
            return MealType.Breakfast;
        if (name.Contains("lunch") || name.Contains("غداء") || name.Contains("غدا"))
            return MealType.Lunch;
        if (name.Contains("dinner") || name.Contains("supper") || name.Contains("عشاء"))
            return MealType.Dinner;
        return MealType.Snack;
    }
}

/// <summary>
/// What a diet version aims for (§4): daily calories and macro grams, and how many meals and snacks.
/// Each value is optional; the coach can take them from the calculator (§14, §15).
/// </summary>
public sealed record DietTargets(
    decimal? Calories = null,
    decimal? Protein = null,
    decimal? Carbohydrates = null,
    decimal? Fat = null,
    int? MealCount = null,
    int? SnackCount = null)
{
    public static DietTargets None { get; } = new();

    public DietTargets Validate()
    {
        Range(Calories, 0m, 20000m, "targets.calories");
        Range(Protein, 0m, 2000m, "targets.protein");
        Range(Carbohydrates, 0m, 2000m, "targets.carbohydrates");
        Range(Fat, 0m, 1000m, "targets.fat");
        if (MealCount is < 0 or > 15) throw new ArgumentException("Must be between 0 and 15.", "targets.mealCount");
        if (SnackCount is < 0 or > 15) throw new ArgumentException("Must be between 0 and 15.", "targets.snackCount");
        return this;
    }

    private static void Range(decimal? value, decimal min, decimal max, string name)
    {
        if (value is { } amount && (amount < min || amount > max))
            throw new ArgumentException($"Must be between {min} and {max}.", name);
    }
}
