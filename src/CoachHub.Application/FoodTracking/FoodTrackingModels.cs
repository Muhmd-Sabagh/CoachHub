using CoachHub.Application.DietPlanning;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;

namespace CoachHub.Application.FoodTracking;

/// <summary>Calories and macros (and fiber) for a day, a meal or an entry.</summary>
public sealed record MacroAmounts(decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber = 0)
{
    public static readonly MacroAmounts Zero = new(0, 0, 0, 0, 0);

    public static MacroAmounts From(NutritionTotals totals) =>
        new(totals.Calories, totals.Protein, totals.Carbohydrates, totals.Fat, totals.Fiber);

    public static MacroAmounts From(FoodLogNutrients nutrients) =>
        new(nutrients.Calories, nutrients.Protein, nutrients.Carbohydrates, nutrients.Fat, nutrients.Fiber);

    public static MacroAmounts operator +(MacroAmounts left, MacroAmounts right) => new(
        left.Calories + right.Calories, left.Protein + right.Protein, left.Carbohydrates + right.Carbohydrates,
        left.Fat + right.Fat, left.Fiber + right.Fiber);

    public static MacroAmounts operator -(MacroAmounts left, MacroAmounts right) => new(
        left.Calories - right.Calories, left.Protein - right.Protein, left.Carbohydrates - right.Carbohydrates,
        left.Fat - right.Fat, left.Fiber - right.Fiber);

    public MacroAmounts Rounded() => new(
        decimal.Round(Calories, 1), decimal.Round(Protein, 1), decimal.Round(Carbohydrates, 1),
        decimal.Round(Fat, 1), decimal.Round(Fiber, 1));

    public MacroAmounts DividedBy(int count) => count <= 0 ? Zero : new MacroAmounts(
        Calories / count, Protein / count, Carbohydrates / count, Fat / count, Fiber / count).Rounded();
}

/// <summary>The client's choices for a day: the diet variant, and an option per flexible meal (null = the first).</summary>
public sealed record NutritionDayChoicesInput(Guid? VariantId, IReadOnlyCollection<MealOptionChoiceInput>? MealOptions = null);
public sealed record MealOptionChoiceInput(Guid MealId, Guid? OptionId);

/// <summary>
/// A diary entry (§8). Exactly one of: a <paramref name="Custom"/> food; a planned row
/// (<paramref name="PlannedMealFoodItemId"/>, optionally with the <paramref name="ReplacementOptionId"/> eaten
/// instead); or a catalog <paramref name="FoodItemId"/> as an extra. <paramref name="Quantity"/> is in
/// <paramref name="FoodUnitId"/> or the base unit; planned foods and alternatives default to their planned quantity.
/// </summary>
public sealed record FoodLogEntryInput(
    DateOnly Date,
    MealType Slot,
    Guid? FoodItemId = null,
    decimal? Quantity = null,
    Guid? FoodUnitId = null,
    Guid? PlannedMealFoodItemId = null,
    Guid? ReplacementOptionId = null,
    CustomFoodInput? Custom = null);

/// <summary>A food that is not in the catalog, with the nutrients of the portion eaten.</summary>
public sealed record CustomFoodInput(
    string NameEn, decimal Calories, decimal Protein = 0, decimal Carbohydrates = 0, decimal Fat = 0, decimal Fiber = 0);

/// <summary>
/// Changes to an entry: its slot; for a catalog food its quantity (in <paramref name="FoodUnitId"/>, or the base
/// unit); for a custom entry its name and nutrients.
/// </summary>
public sealed record FoodLogEntryUpdate(MealType? Slot = null, decimal? Quantity = null, Guid? FoodUnitId = null, CustomFoodInput? Custom = null);

public sealed record FoodLogEntryResponse(
    Guid Id, DateOnly Date, MealType Slot, FoodLogSource Source, string NameEn, string? NameAr, bool IsCustom,
    Guid? FoodItemId, decimal? Quantity, Guid? FoodUnitId, string? UnitLabel, string? UnitLabelAr,
    MacroAmounts Nutrients, Guid? PlannedMealId, Guid? PlannedMealFoodItemId, Guid? ReplacementOptionId,
    DateTimeOffset LoggedAt);

/// <summary>
/// A day of the client's nutrition: the plan in force (§4, §13), what was logged (§8), and the planned versus
/// actual summary (§9, §10). <paramref name="Plan"/> is null when no diet applies to the day.
/// </summary>
public sealed record NutritionDayResponse(
    DateOnly Date, PlannedDayResponse? Plan, IReadOnlyList<FoodLogEntryResponse> Entries, NutritionDaySummary Summary);

/// <summary>The diet version in force on the day and the variant the client follows.</summary>
public sealed record PlannedDayResponse(
    Guid PlanId, string NameEn, string? NameAr, int VersionNumber, Guid VariantId,
    IReadOnlyList<PlannedVariantResponse> Variants, IReadOnlyList<PlannedMealResponse> Meals);
public sealed record PlannedVariantResponse(Guid Id, string NameEn, string? NameAr);
/// <summary>A planned meal with the foods of its chosen option; <paramref name="IsLogged"/> once any of it is logged.</summary>
public sealed record PlannedMealResponse(
    Guid Id, string NameEn, string? NameAr, MealType Type, string? Notes,
    IReadOnlyList<PlannedMealOptionResponse> Options, Guid? ChosenOptionId,
    IReadOnlyList<PlannedFoodResponse> Foods, MacroAmounts Totals, bool IsLogged);
public sealed record PlannedMealOptionResponse(Guid Id, string NameEn, string? NameAr, MacroAmounts Totals);
/// <summary>A planned food; <paramref name="AlternativeCount"/> is how many alternatives the coach permits (§7).</summary>
public sealed record PlannedFoodResponse(
    Guid Id, Guid FoodItemId, string NameEn, string? NameAr, decimal Quantity, Guid? FoodUnitId,
    string UnitLabel, string UnitLabelAr, MacroAmounts Totals, int AlternativeCount, string? Notes);

/// <summary>
/// Target, planned, consumed and remaining (§9, §10). The target is the version's targets, falling back to the
/// planned food for any value not set. Adherence is null until something is logged.
/// </summary>
public sealed record NutritionDaySummary(
    MacroAmounts? Target, MacroAmounts? Planned, MacroAmounts Consumed, MacroAmounts? Remaining,
    int PlannedMeals, int CompletedMeals, decimal? MealCompletionPercent, decimal? AdherencePercent,
    int EntryCount, bool IsCompleted);

/// <summary>An alternative the coach permits for a planned food (§7), with the quantity the client gets.</summary>
public sealed record FoodAlternativeResponse(
    Guid OptionId, Guid GroupId, string GroupTitle, ReplacementMode Mode, Guid FoodItemId,
    string NameEn, string? NameAr, decimal? Quantity, Guid? FoodUnitId, string UnitLabel, string UnitLabelAr,
    bool IsCalculated, MacroAmounts Totals, string? Notes);

/// <summary>Coach nutrition analytics (§11) for a date range: per day, per week (Monday first) and per month.</summary>
public sealed record NutritionAnalyticsResponse(
    DateOnly From, DateOnly To, NutritionPeriodSummary Summary,
    IReadOnlyList<NutritionDayRow> Days, IReadOnlyList<NutritionPeriodSummary> Weeks, IReadOnlyList<NutritionPeriodSummary> Months);

public sealed record NutritionDayRow(
    DateOnly Date, MacroAmounts? Target, MacroAmounts? Planned, MacroAmounts Consumed,
    decimal? AdherencePercent, decimal? MealCompletionPercent, int EntryCount, bool IsCompleted);

/// <summary>
/// A period's figures. <paramref name="Days"/> counts days up to today; averages and adherence cover logged
/// days; logging adherence is the share of days with anything logged.
/// </summary>
public sealed record NutritionPeriodSummary(
    DateOnly Start, DateOnly End, int Days, int LoggedDays, int CompletedDays,
    MacroAmounts? AverageConsumed, MacroAmounts? AverageTarget,
    decimal? AdherencePercent, decimal? LoggingAdherencePercent);
