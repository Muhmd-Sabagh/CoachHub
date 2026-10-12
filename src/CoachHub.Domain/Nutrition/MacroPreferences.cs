using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Nutrition;

/// <summary>
/// How one client's macros are derived (§15), chosen by the coach. Only the parameters of the chosen
/// mode are kept:
/// <list type="bullet">
/// <item>Mode 1 (<see cref="MacroMode.GramsPerKg"/>): protein and fat in g/kg; either may be left
/// empty to use the coach's default.</item>
/// <item>Mode 2 (<see cref="MacroMode.Percentages"/>): protein, carbohydrate and fat percentages that
/// add up to 100.</item>
/// <item>Mode 3 (<see cref="MacroMode.FixedGrams"/>): fixed protein and fat grams.</item>
/// </list>
/// Carbohydrates fill the remaining calories in modes 1 and 3.
/// </summary>
public sealed record MacroPreferences(
    MacroMode Mode,
    decimal? ProteinGramsPerKg = null,
    decimal? FatGramsPerKg = null,
    decimal? ProteinPercent = null,
    decimal? CarbohydratePercent = null,
    decimal? FatPercent = null,
    decimal? ProteinGrams = null,
    decimal? FatGrams = null)
{
    /// <summary>Validates the chosen mode and drops the parameters of the other modes.</summary>
    public MacroPreferences Normalize()
    {
        switch (Mode)
        {
            case MacroMode.GramsPerKg:
                Range(ProteinGramsPerKg, 0m, 5m, "macros.proteinGramsPerKg");
                Range(FatGramsPerKg, 0m, 3m, "macros.fatGramsPerKg");
                return new(Mode, ProteinGramsPerKg, FatGramsPerKg);
            case MacroMode.Percentages:
                if (ProteinPercent is not { } protein || CarbohydratePercent is not { } carbohydrates || FatPercent is not { } fat)
                    throw new ArgumentException("Percentages need protein, carbohydrate and fat percentages.", "macros");
                Range(protein, 0m, 100m, "macros.proteinPercent");
                Range(carbohydrates, 0m, 100m, "macros.carbohydratePercent");
                Range(fat, 0m, 100m, "macros.fatPercent");
                if (protein + carbohydrates + fat != 100m)
                    throw new ArgumentException("Macro percentages must add up to 100.", "macros");
                return new(Mode, ProteinPercent: protein, CarbohydratePercent: carbohydrates, FatPercent: fat);
            case MacroMode.FixedGrams:
                if (ProteinGrams is not { } proteinGrams || FatGrams is not { } fatGrams)
                    throw new ArgumentException("Fixed grams need protein and fat grams.", "macros");
                Range(proteinGrams, 0m, 1000m, "macros.proteinGrams");
                Range(fatGrams, 0m, 500m, "macros.fatGrams");
                return new(Mode, ProteinGrams: proteinGrams, FatGrams: fatGrams);
            default:
                throw new ArgumentException("Unknown macro mode.", "macros.mode");
        }
    }

    private static void Range(decimal? value, decimal min, decimal max, string name)
    {
        if (value is { } amount && (amount < min || amount > max))
            throw new ArgumentException($"Must be between {min} and {max}.", name);
    }
}
