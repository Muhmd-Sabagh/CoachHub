using System.Text.Json.Serialization;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;
namespace CoachHub.Application.DietPlanning;

/// <summary>
/// A diet plan's name and assignment plus the content of one version (D4): its program dates (§4),
/// Draft/Active status, notes and variants. <paramref name="BaseVersionNumber"/> is the version the
/// editor loaded; a save based on an older version is rejected with 409. <paramref name="Targets"/> are
/// the version's calorie, macro, meal and snack targets (§4).
/// </summary>
public sealed record DietPlanInput(
    string NameEn,
    string? NameAr,
    Guid? ClientId,
    IReadOnlyCollection<DietPlanNoteInput> Notes,
    IReadOnlyCollection<DietPlanVariantInput> Variants,
    Guid? CoachId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    PlanRevisionStatus Status = PlanRevisionStatus.Active,
    int? BaseVersionNumber = null,
    DietTargets? Targets = null);

public sealed record DietPlanNoteInput(Guid Id, string Text, int Order, bool IsActive);
/// <summary>An ordered tab of the version (for example "Training day"), formerly "version".</summary>
public sealed record DietPlanVariantInput(
    Guid Id, string NameEn, string? NameAr, int Order, bool IsActiveForPdf, string? Notes,
    IReadOnlyCollection<MealInput> Meals,
    IReadOnlyCollection<DietReplacementGroupInput> ReplacementGroups);
/// <summary>
/// A meal; without a <paramref name="Type"/> it is guessed from the name (anything else is a snack). With
/// <paramref name="Options"/> (§13: "Option A", "Option B") every food row names its option.
/// </summary>
public sealed record MealInput(
    Guid Id, string NameEn, string? NameAr, int Order, string? Notes,
    IReadOnlyCollection<MealFoodItemInput> FoodItems, MealType? Type = null,
    IReadOnlyCollection<MealOptionInput>? Options = null);
/// <summary>An option of a flexible meal (§13); the first by order is the meal's default.</summary>
public sealed record MealOptionInput(Guid Id, string NameEn, string? NameAr, int Order);
/// <summary>
/// A food in a meal; <paramref name="Quantity"/> is in <paramref name="FoodUnitId"/> (one of the food's units) or its
/// base unit. <paramref name="MealOptionId"/> is required when the meal has options.
/// </summary>
public sealed record MealFoodItemInput(
    Guid Id, Guid FoodItemId, decimal Quantity, int Order, string? Notes, Guid? FoodUnitId = null, Guid? MealOptionId = null);
/// <summary>
/// The alternatives permitted for a food row, or a whole meal (§7). In <see cref="ReplacementMode.Flexible"/> mode
/// a food's quantity is calculated unless the coach sets one.
/// </summary>
public sealed record DietReplacementGroupInput(
    Guid Id, Guid TargetMealId, Guid? TargetMealFoodItemId, string Title, int Order,
    IReadOnlyCollection<DietReplacementOptionInput> Options, ReplacementMode Mode = ReplacementMode.Exact);
public sealed record DietReplacementOptionInput(
    Guid Id, Guid? ReplacementFoodItemId, Guid? ReplacementMealId,
    decimal? Quantity, int Order, string? Notes, Guid? FoodUnitId = null);

public sealed record CopyDietPlanInput(string NameEn, string? NameAr, Guid? ClientId);
public sealed record AssignDietPlanInput(Guid? ClientId);
public sealed record SetDietPlanNoteActiveInput(bool IsActive);

/// <summary>Nutrients of a food row, meal, variant or plan (§6). Weight is the sum of base-unit amounts.</summary>
public sealed record NutritionTotals(
    decimal Weight, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber = 0)
{
    public static readonly NutritionTotals Zero = new(0, 0, 0, 0, 0, 0);
    public static NutritionTotals operator +(NutritionTotals left, NutritionTotals right) => new(
        left.Weight + right.Weight,
        left.Calories + right.Calories,
        left.Protein + right.Protein,
        left.Carbohydrates + right.Carbohydrates,
        left.Fat + right.Fat,
        left.Fiber + right.Fiber);
}

/// <summary>A diet plan as one of its versions: the current one, or an archived one (read-only).</summary>
public sealed record DietPlanResponse(
    Guid Id, Guid CoachId, string NameEn, string? NameAr, Guid? ClientId, DateTimeOffset CreatedAt,
    PlanRevisionInfo Revision, int CurrentVersionNumber, DietTargets Targets,
    IReadOnlyList<DietPlanNoteResponse> Notes,
    IReadOnlyList<DietPlanVariantResponse> Variants,
    NutritionTotals Totals);
public sealed record DietPlanNoteResponse(Guid Id, string Text, int Order, bool IsActive);
public sealed record DietPlanVariantResponse(
    Guid Id, string NameEn, string? NameAr, int Order, bool IsActiveForPdf, string? Notes,
    IReadOnlyList<MealResponse> Meals,
    IReadOnlyList<DietReplacementGroupResponse> ReplacementGroups,
    NutritionTotals Totals);
/// <summary>A meal; with options, <paramref name="Totals"/> are those of its first (default) option.</summary>
public sealed record MealResponse(
    Guid Id, string NameEn, string? NameAr, MealType Type, int Order, string? Notes,
    IReadOnlyList<MealFoodItemResponse> FoodItems, NutritionTotals Totals,
    IReadOnlyList<MealOptionResponse> Options);
public sealed record MealOptionResponse(Guid Id, string NameEn, string? NameAr, int Order, NutritionTotals Totals);
/// <summary>A food row; <paramref name="UnitLabel"/> names the unit the quantity is in ("g", "slice", "cup").</summary>
public sealed record MealFoodItemResponse(
    Guid Id, Guid FoodItemId, string FoodNameEn, string? FoodNameAr, Guid? FoodUnitId, string UnitLabel, string UnitLabelAr,
    decimal Quantity, int Order, string? Notes, NutritionTotals Totals, Guid? MealOptionId = null);
public sealed record DietReplacementGroupResponse(
    Guid Id, Guid TargetMealId, Guid? TargetMealFoodItemId, string Title, int Order,
    IReadOnlyList<DietReplacementOptionResponse> Options, ReplacementMode Mode);
/// <summary>
/// An alternative. <paramref name="Quantity"/> is what the coach set; <paramref name="EffectiveQuantity"/> is what the
/// client gets (the calculated one in a flexible group without an override), and <paramref name="Totals"/> are for it.
/// </summary>
public sealed record DietReplacementOptionResponse(
    Guid Id, Guid? ReplacementFoodItemId, Guid? ReplacementMealId,
    string ReplacementNameEn, string? ReplacementNameAr,
    decimal? Quantity, int Order, string? Notes, NutritionTotals Totals,
    Guid? FoodUnitId, string? UnitLabel, string? UnitLabelAr, decimal? EffectiveQuantity, bool IsCalculated);

[JsonConverter(typeof(JsonStringEnumConverter<BiologicalSex>))]
public enum BiologicalSex { Male, Female }
[JsonConverter(typeof(JsonStringEnumConverter<EnergyGoal>))]
public enum EnergyGoal { LoseWeight, Maintain, GainWeight }
/// <summary>
/// An ad-hoc calculation (§14, §15) with the coach's coefficients. Without <paramref name="Macros"/> the
/// coach's g/kg defaults apply. <paramref name="CoachId"/> lets an administrator use a coach's settings.
/// </summary>
public sealed record EnergyCalculatorInput(
    int Age, BiologicalSex Sex, decimal WeightKg, decimal HeightCm,
    ActivityLevel ActivityLevel, EnergyGoal Goal,
    MacroPreferences? Macros = null, Guid? CoachId = null);
/// <summary>
/// Calorie and macro targets with the inputs and coefficients used: the activity factor and the
/// deficit (negative) or surplus percentage applied to maintenance.
/// </summary>
public sealed record EnergyCalculatorResponse(
    decimal WeightKg, int Age, ActivityLevel ActivityLevel, decimal ActivityFactor, EnergyGoal Goal,
    decimal AdjustmentPercent, decimal BasalMetabolicRate, decimal MaintenanceCalories,
    decimal GoalCalories, MacroPreferences Macros, decimal Protein, decimal Carbohydrates, decimal Fat);
