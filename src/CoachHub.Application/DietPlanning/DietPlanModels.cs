namespace CoachHub.Application.DietPlanning;

public sealed record DietPlanInput(
    string NameEn,
    string? NameAr,
    Guid? ClientId,
    IReadOnlyCollection<DietPlanNoteInput> Notes,
    IReadOnlyCollection<DietPlanVersionInput> Versions);

public sealed record DietPlanNoteInput(Guid Id, string Text, int Order, bool IsActive);
public sealed record DietPlanVersionInput(
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

public sealed record DietPlanResponse(
    Guid Id, string NameEn, string? NameAr, Guid? ClientId, DateTimeOffset CreatedAt,
    IReadOnlyList<DietPlanNoteResponse> Notes,
    IReadOnlyList<DietPlanVersionResponse> Versions,
    NutritionTotals Totals);
public sealed record DietPlanNoteResponse(Guid Id, string Text, int Order, bool IsActive);
public sealed record DietPlanVersionResponse(
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

public enum BiologicalSex { Male, Female }
public enum EnergyGoal { LoseWeight, Maintain, GainWeight }
public sealed record EnergyCalculatorInput(
    int Age, BiologicalSex Sex, decimal WeightKg, decimal HeightCm,
    decimal ActivityFactor, EnergyGoal Goal,
    decimal ProteinGramsPerKg = 2m, decimal FatCaloriesPercent = 0.25m);
public sealed record EnergyCalculatorResponse(
    decimal WeightKg, decimal BasalMetabolicRate, decimal MaintenanceCalories,
    decimal GoalCalories, decimal Protein, decimal Carbohydrates, decimal Fat);
