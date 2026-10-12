using CoachHub.Domain.Common;

namespace CoachHub.Domain.Settings;

/// <summary>Activity levels; each coach sets the multiplier for each (§14, §49).</summary>
public enum ActivityLevel
{
    Sedentary,
    LightlyActive,
    ModeratelyActive,
    VeryActive,
    ExtraActive
}

/// <summary>How macronutrient targets are derived (§15).</summary>
public enum MacroMode
{
    /// <summary>Mode 1: protein and fat in g/kg of body weight; carbohydrates fill the rest.</summary>
    GramsPerKg,

    /// <summary>Mode 2: percentages of the calorie target.</summary>
    Percentages,

    /// <summary>Mode 3: fixed grams set by the coach.</summary>
    FixedGrams
}

/// <summary>How an equivalent replacement quantity is matched (§7).</summary>
public enum FoodReplacementRule
{
    Calories,
    DominantMacro
}

/// <summary>
/// A coach's calculation defaults (§49), typed (D3): activity factors, calorie adjustment,
/// macro defaults, adherence weights and the replacement rule. Consumed by the nutrition engine
/// (P7, P8) and adherence scoring (P12). A coach without a row uses <see cref="CreateDefault"/>.
/// </summary>
public sealed class CoachSettings : Entity, ITenantOwned
{
    private CoachSettings() { }

    public Guid CoachId { get; private set; }

    public decimal SedentaryFactor { get; private set; }
    public decimal LightlyActiveFactor { get; private set; }
    public decimal ModeratelyActiveFactor { get; private set; }
    public decimal VeryActiveFactor { get; private set; }
    public decimal ExtraActiveFactor { get; private set; }

    /// <summary>Calorie deficit for weight loss, as a percentage of maintenance.</summary>
    public decimal DeficitPercent { get; private set; }

    /// <summary>Calorie surplus for weight gain, as a percentage of maintenance.</summary>
    public decimal SurplusPercent { get; private set; }
    public decimal ProteinGramsPerKg { get; private set; }
    public decimal FatGramsPerKg { get; private set; }
    public MacroMode DefaultMacroMode { get; private set; }

    // Adherence weights in percent; they add up to 100 (§30).
    public int NutritionWeight { get; private set; }
    public int WorkoutWeight { get; private set; }
    public int CheckInWeight { get; private set; }
    public int StepsWeight { get; private set; }
    public int WaterWeight { get; private set; }
    public int SleepWeight { get; private set; }

    public FoodReplacementRule ReplacementRule { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Common defaults: Mifflin-St Jeor activity multipliers, 20% deficit, 10% surplus, 2.0/0.8 g/kg.</summary>
    public static CoachSettings CreateDefault(Guid coachId, DateTimeOffset now)
    {
        var settings = new CoachSettings { CoachId = TenantKey.Required(coachId) };
        settings.Update(
            new ActivityFactors(1.2m, 1.375m, 1.55m, 1.725m, 1.9m),
            20m, 10m, 2.0m, 0.8m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5),
            FoodReplacementRule.Calories,
            now);
        return settings;
    }

    public void Update(
        ActivityFactors factors,
        decimal deficitPercent,
        decimal surplusPercent,
        decimal proteinGramsPerKg,
        decimal fatGramsPerKg,
        MacroMode defaultMacroMode,
        AdherenceWeights weights,
        FoodReplacementRule replacementRule,
        DateTimeOffset now)
    {
        factors.Validate();
        weights.Validate();
        Range(deficitPercent, 0m, 50m, nameof(deficitPercent));
        Range(surplusPercent, 0m, 50m, nameof(surplusPercent));
        Range(proteinGramsPerKg, 0m, 5m, nameof(proteinGramsPerKg));
        Range(fatGramsPerKg, 0m, 3m, nameof(fatGramsPerKg));
        if (!Enum.IsDefined(defaultMacroMode)) throw new ArgumentException("Unknown macro mode.", nameof(defaultMacroMode));
        if (!Enum.IsDefined(replacementRule)) throw new ArgumentException("Unknown replacement rule.", nameof(replacementRule));

        (SedentaryFactor, LightlyActiveFactor, ModeratelyActiveFactor, VeryActiveFactor, ExtraActiveFactor) =
            (factors.Sedentary, factors.LightlyActive, factors.ModeratelyActive, factors.VeryActive, factors.ExtraActive);
        DeficitPercent = deficitPercent;
        SurplusPercent = surplusPercent;
        ProteinGramsPerKg = proteinGramsPerKg;
        FatGramsPerKg = fatGramsPerKg;
        DefaultMacroMode = defaultMacroMode;
        (NutritionWeight, WorkoutWeight, CheckInWeight, StepsWeight, WaterWeight, SleepWeight) =
            (weights.Nutrition, weights.Workout, weights.CheckIn, weights.Steps, weights.Water, weights.Sleep);
        ReplacementRule = replacementRule;
        UpdatedAt = now;
    }

    public ActivityFactors Factors => new(SedentaryFactor, LightlyActiveFactor, ModeratelyActiveFactor, VeryActiveFactor, ExtraActiveFactor);
    public AdherenceWeights Weights => new(NutritionWeight, WorkoutWeight, CheckInWeight, StepsWeight, WaterWeight, SleepWeight);

    public decimal FactorFor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => SedentaryFactor,
        ActivityLevel.LightlyActive => LightlyActiveFactor,
        ActivityLevel.ModeratelyActive => ModeratelyActiveFactor,
        ActivityLevel.VeryActive => VeryActiveFactor,
        ActivityLevel.ExtraActive => ExtraActiveFactor,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    private static void Range(decimal value, decimal min, decimal max, string name)
    {
        if (value < min || value > max) throw new ArgumentException($"Must be between {min} and {max}.", name);
    }
}

/// <summary>Activity multipliers applied to BMR; each between 1.0 and 2.5, rising with activity.</summary>
public sealed record ActivityFactors(
    decimal Sedentary, decimal LightlyActive, decimal ModeratelyActive, decimal VeryActive, decimal ExtraActive)
{
    public void Validate()
    {
        decimal[] values = [Sedentary, LightlyActive, ModeratelyActive, VeryActive, ExtraActive];
        if (values.Any(value => value is < 1m or > 2.5m))
            throw new ArgumentException("Each activity factor must be between 1.0 and 2.5.", "activityFactors");
        if (values.Zip(values.Skip(1)).Any(pair => pair.First > pair.Second))
            throw new ArgumentException("Activity factors must not decrease as activity rises.", "activityFactors");
    }
}

/// <summary>Weights of each adherence area in the overall score, in percent; they add up to 100.</summary>
public sealed record AdherenceWeights(int Nutrition, int Workout, int CheckIn, int Steps, int Water, int Sleep)
{
    public void Validate()
    {
        int[] values = [Nutrition, Workout, CheckIn, Steps, Water, Sleep];
        if (values.Any(value => value is < 0 or > 100) || values.Sum() != 100)
            throw new ArgumentException("Adherence weights must each be 0–100 and add up to 100.", "adherenceWeights");
    }
}
