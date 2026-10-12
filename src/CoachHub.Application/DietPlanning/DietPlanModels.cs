using System.Text.Json.Serialization;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Common;
namespace CoachHub.Application.DietPlanning;

/// <summary>
/// A diet plan's name and assignment plus the content of one version (D4): its program dates (§4),
/// Draft/Active status, notes and variants. <paramref name="BaseVersionNumber"/> is the version the
/// editor loaded; a save based on an older version is rejected with 409.
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
    int? BaseVersionNumber = null);

public sealed record DietPlanNoteInput(Guid Id, string Text, int Order, bool IsActive);
/// <summary>An ordered tab of the version (for example "Training day"), formerly "version".</summary>
public sealed record DietPlanVariantInput(
    Guid Id, string NameEn, string? NameAr, int Order, bool IsActiveForPdf, string? Notes,
    IReadOnlyCollection<MealInput> Meals,
    IReadOnlyCollection<DietReplacementGroupInput> ReplacementGroups);
public sealed record MealInput(
    Guid Id, string NameEn, string? NameAr, int Order, string? Notes,
    IReadOnlyCollection<MealFoodItemInput> FoodItems);
public sealed record MealFoodItemInput(
    Guid Id, Guid FoodItemId, decimal Quantity, int Order, string? Notes);
public sealed record DietReplacementGroupInput(
    Guid Id, Guid TargetMealId, Guid? TargetMealFoodItemId, string Title, int Order,
    IReadOnlyCollection<DietReplacementOptionInput> Options);
public sealed record DietReplacementOptionInput(
    Guid Id, Guid? ReplacementFoodItemId, Guid? ReplacementMealId,
    decimal? Quantity, int Order, string? Notes);

public sealed record CopyDietPlanInput(string NameEn, string? NameAr, Guid? ClientId);
public sealed record AssignDietPlanInput(Guid? ClientId);
public sealed record SetDietPlanNoteActiveInput(bool IsActive);

public sealed record NutritionTotals(
    decimal Weight, decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat)
{
    public static readonly NutritionTotals Zero = new(0, 0, 0, 0, 0);
    public static NutritionTotals operator +(NutritionTotals left, NutritionTotals right) => new(
        left.Weight + right.Weight,
        left.Calories + right.Calories,
        left.Protein + right.Protein,
        left.Carbohydrates + right.Carbohydrates,
        left.Fat + right.Fat);
}

/// <summary>A diet plan as one of its versions: the current one, or an archived one (read-only).</summary>
public sealed record DietPlanResponse(
    Guid Id, Guid CoachId, string NameEn, string? NameAr, Guid? ClientId, DateTimeOffset CreatedAt,
    PlanRevisionInfo Revision, int CurrentVersionNumber,
    IReadOnlyList<DietPlanNoteResponse> Notes,
    IReadOnlyList<DietPlanVariantResponse> Variants,
    NutritionTotals Totals);
public sealed record DietPlanNoteResponse(Guid Id, string Text, int Order, bool IsActive);
public sealed record DietPlanVariantResponse(
    Guid Id, string NameEn, string? NameAr, int Order, bool IsActiveForPdf, string? Notes,
    IReadOnlyList<MealResponse> Meals,
    IReadOnlyList<DietReplacementGroupResponse> ReplacementGroups,
    NutritionTotals Totals);
public sealed record MealResponse(
    Guid Id, string NameEn, string? NameAr, int Order, string? Notes,
    IReadOnlyList<MealFoodItemResponse> FoodItems, NutritionTotals Totals);
public sealed record MealFoodItemResponse(
    Guid Id, Guid FoodItemId, string FoodNameEn, string? FoodNameAr, string MeasurementUnit,
    decimal Quantity, int Order, string? Notes, NutritionTotals Totals);
public sealed record DietReplacementGroupResponse(
    Guid Id, Guid TargetMealId, Guid? TargetMealFoodItemId, string Title, int Order,
    IReadOnlyList<DietReplacementOptionResponse> Options);
public sealed record DietReplacementOptionResponse(
    Guid Id, Guid? ReplacementFoodItemId, Guid? ReplacementMealId,
    string ReplacementNameEn, string? ReplacementNameAr,
    decimal? Quantity, int Order, string? Notes, NutritionTotals Totals);

[JsonConverter(typeof(JsonStringEnumConverter<BiologicalSex>))]
public enum BiologicalSex { Male, Female }
[JsonConverter(typeof(JsonStringEnumConverter<EnergyGoal>))]
public enum EnergyGoal { LoseWeight, Maintain, GainWeight }
public sealed record EnergyCalculatorInput(
    int Age, BiologicalSex Sex, decimal WeightKg, decimal HeightCm,
    decimal ActivityFactor, EnergyGoal Goal,
    decimal ProteinGramsPerKg = 2m, decimal FatCaloriesPercent = 0.25m);
public sealed record EnergyCalculatorResponse(
    decimal WeightKg, decimal BasalMetabolicRate, decimal MaintenanceCalories,
    decimal GoalCalories, decimal Protein, decimal Carbohydrates, decimal Fat);
