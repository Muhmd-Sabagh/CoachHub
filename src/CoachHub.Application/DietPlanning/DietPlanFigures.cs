using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.DietPlanning;

/// <summary>
/// The planned nutrition of one plan version (§6, §7, §13): food rows, meals and their options, and the
/// quantity each alternative gives. A meal with options counts its chosen option, by default the first.
/// Shared by the plan editor and the client's diary so both show the same planned values.
/// </summary>
public sealed class DietPlanFigures
{
    private readonly DietPlanAggregate _aggregate;
    private readonly IReadOnlyDictionary<Guid, FoodItem> _foods;
    private readonly FoodReplacementRule _rule;
    private readonly Dictionary<Guid, NutritionTotals> _rows;
    private readonly ILookup<Guid, MealOption> _options;

    public DietPlanFigures(DietPlanAggregate aggregate, IReadOnlyDictionary<Guid, FoodItem> foods, FoodReplacementRule rule)
    {
        _aggregate = aggregate;
        _foods = foods;
        _rule = rule;
        _rows = aggregate.FoodItems.ToDictionary(
            row => row.Id, row => NutritionCalculator.Calculate(foods[row.FoodItemId], row.Quantity, row.FoodUnitId));
        _options = aggregate.MealOptions.OrderBy(x => x.Order).ToLookup(x => x.MealId);
    }

    public FoodItem Food(Guid foodId) => _foods[foodId];

    public NutritionTotals Row(Guid rowId) => _rows[rowId];

    /// <summary>The meal's options in order; empty for a meal without options.</summary>
    public IReadOnlyList<MealOption> OptionsOf(Guid mealId) => _options[mealId].ToArray();

    /// <summary>The option that counts: <paramref name="chosen"/> when it is one of the meal's, otherwise the first.</summary>
    public Guid? OptionFor(Guid mealId, Guid? chosen = null)
    {
        var options = _options[mealId].ToArray();
        if (options.Length == 0) return null;
        return chosen is { } id && options.Any(x => x.Id == id) ? id : options[0].Id;
    }

    /// <summary>The rows eaten for the meal: all of them, or those of the option that counts.</summary>
    public IReadOnlyList<MealFoodItem> RowsOf(Guid mealId, Guid? chosenOption = null)
    {
        var option = OptionFor(mealId, chosenOption);
        return _aggregate.FoodItems.Where(x => x.MealId == mealId && x.MealOptionId == option).OrderBy(x => x.Order).ToArray();
    }

    public NutritionTotals Meal(Guid mealId, Guid? chosenOption = null) => Sum(RowsOf(mealId, chosenOption));

    public NutritionTotals Option(Guid optionId) =>
        Sum(_aggregate.FoodItems.Where(x => x.MealOptionId == optionId));

    /// <summary>
    /// The quantity an alternative gives, and whether it was calculated: the coach's quantity when set; in a
    /// flexible group otherwise the equivalent of the targeted row (or the meal's default option). Null for a
    /// whole-meal alternative, or when nothing can be matched.
    /// </summary>
    public (decimal? Quantity, bool Calculated) Quantity(DietReplacementOption option)
    {
        if (option.ReplacementFoodItemId is not { } foodId) return (null, false);
        if (option.Quantity is { } set) return (set, false);
        var group = _aggregate.ReplacementGroups.Single(x => x.Id == option.DietReplacementGroupId);
        if (group.Mode != ReplacementMode.Flexible) return (null, false);
        var original = group.TargetMealFoodItemId is { } rowId ? Row(rowId) : Meal(group.TargetMealId);
        return (ReplacementCalculator.EquivalentQuantity(original, _foods[foodId], option.FoodUnitId, _rule), true);
    }

    /// <summary>The nutrients of an alternative at its quantity; a whole-meal alternative counts that meal.</summary>
    public NutritionTotals Alternative(DietReplacementOption option)
    {
        if (option.ReplacementMealId is { } mealId) return Meal(mealId);
        return Quantity(option).Quantity is { } quantity
            ? NutritionCalculator.Calculate(_foods[option.ReplacementFoodItemId!.Value], quantity, option.FoodUnitId)
            : NutritionTotals.Zero;
    }

    private NutritionTotals Sum(IEnumerable<MealFoodItem> rows) =>
        NutritionCalculator.Round(rows.Aggregate(NutritionTotals.Zero, (sum, row) => sum + _rows[row.Id]));
}
