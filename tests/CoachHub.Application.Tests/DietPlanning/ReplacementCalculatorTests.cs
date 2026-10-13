using CoachHub.Application.DietPlanning;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Tests.DietPlanning;

/// <summary>Equivalent quantities for flexible alternatives (§7) and planned figures with meal options (§13).</summary>
public sealed class ReplacementCalculatorTests
{
    private static readonly FoodItem Chicken = Food("Chicken breast", 165, 31, 0, 3.6m);
    private static readonly FoodItem Turkey = Food("Turkey", 135, 30, 0, 1);
    private static readonly FoodItem Rice = Food("Rice", 130, 2.7m, 28, 0.3m);
    private static readonly FoodItem Potato = Food("Potato", 77, 2, 17, 0.1m);
    private static readonly FoodItem Oil = Food("Olive oil", 884, 0, 0, 100);

    [Fact]
    public void Calories_rule_matches_the_originals_calories()
    {
        var original = NutritionCalculator.Calculate(Chicken, 150); // 247.5 kcal

        Assert.Equal(183m, ReplacementCalculator.EquivalentQuantity(original, Turkey, null, FoodReplacementRule.Calories));
        Assert.Equal(338m, ReplacementCalculator.EquivalentQuantity(NutritionCalculator.Calculate(Rice, 200), Potato, null, FoodReplacementRule.Calories));
    }

    [Fact]
    public void Dominant_macro_rule_matches_the_macro_that_gives_most_calories()
    {
        var chicken = NutritionCalculator.Calculate(Chicken, 150); // 46.5 g protein
        var rice = NutritionCalculator.Calculate(Rice, 200); // 56 g carbohydrates

        Assert.Equal(ReplacementCalculator.Basis.Protein, ReplacementCalculator.DominantMacro(chicken));
        Assert.Equal(ReplacementCalculator.Basis.Carbohydrates, ReplacementCalculator.DominantMacro(rice));
        Assert.Equal(ReplacementCalculator.Basis.Fat, ReplacementCalculator.DominantMacro(NutritionCalculator.Calculate(Oil, 10)));
        Assert.Equal(155m, ReplacementCalculator.EquivalentQuantity(chicken, Turkey, null, FoodReplacementRule.DominantMacro));
        Assert.Equal(329m, ReplacementCalculator.EquivalentQuantity(rice, Potato, null, FoodReplacementRule.DominantMacro));
        // Oil has no protein, so the match falls back to calories: 247.5 / 8.84 ≈ 28 g.
        Assert.Equal(28m, ReplacementCalculator.EquivalentQuantity(chicken, Oil, null, FoodReplacementRule.DominantMacro));
    }

    [Fact]
    public void Named_units_and_pieces_round_to_halves()
    {
        var oats = FoodItem.Create(null, new FoodValues("Oats", null, Guid.NewGuid(), FoodBaseUnit.Gram, 80, 389, 16.9m, 66.3m, 6.9m),
            [new FoodUnitValues(null, "cup", "كوب", 80)]);
        var eggs = FoodItem.Create(null, new FoodValues("Egg", null, Guid.NewGuid(), FoodBaseUnit.Piece, 1, 7150, 600, 50, 500,
            PieceNameEn: "egg"));
        var rice = NutritionCalculator.Calculate(Rice, 200); // 260 kcal

        // 260 kcal of oats is 66.8 g, 0.84 of a cup → one cup; of eggs (71.5 kcal each) 3.6 → 3.5 eggs.
        Assert.Equal(1m, ReplacementCalculator.EquivalentQuantity(rice, oats, oats.Units.Single().Id, FoodReplacementRule.Calories));
        Assert.Equal(3.5m, ReplacementCalculator.EquivalentQuantity(rice, eggs, null, FoodReplacementRule.Calories));
        Assert.Throws<ArgumentException>(() => ReplacementCalculator.EquivalentQuantity(rice, eggs, Guid.NewGuid(), FoodReplacementRule.Calories));
    }

    [Fact]
    public void Nothing_to_match_gives_no_quantity()
    {
        var water = Food("Water", 0, 0, 0, 0);

        Assert.Null(ReplacementCalculator.EquivalentQuantity(NutritionCalculator.Calculate(water, 500), Turkey, null, FoodReplacementRule.Calories));
        Assert.Null(ReplacementCalculator.EquivalentQuantity(NutritionCalculator.Calculate(Chicken, 100), water, null, FoodReplacementRule.DominantMacro));
    }

    [Fact]
    public void Plan_figures_count_the_chosen_option_and_calculate_flexible_alternatives()
    {
        var now = DateTimeOffset.UtcNow;
        var plan = DietPlan.Create(Guid.NewGuid(), "Plan", null, null, now);
        var revision = DietPlanRevision.Create(plan.Id, 1, PlanRevisionStatus.Active, null, null, null, now);
        var variant = DietPlanVariant.Create(Guid.NewGuid(), revision.Id, "Day", null, 0, true, null);
        var breakfast = Meal.Create(Guid.NewGuid(), revision.Id, variant.Id, "Breakfast", null, 0, null);
        var lunch = Meal.Create(Guid.NewGuid(), revision.Id, variant.Id, "Lunch", null, 1, null);
        var optionA = MealOption.Create(Guid.NewGuid(), revision.Id, breakfast.Id, "Option A", null, 0);
        var optionB = MealOption.Create(Guid.NewGuid(), revision.Id, breakfast.Id, "Option B", null, 1);
        var riceRow = MealFoodItem.Create(Guid.NewGuid(), revision.Id, breakfast.Id, Rice.Id, 100, 0, null, mealOptionId: optionA.Id);
        var potatoRow = MealFoodItem.Create(Guid.NewGuid(), revision.Id, breakfast.Id, Potato.Id, 100, 1, null, mealOptionId: optionB.Id);
        var chickenRow = MealFoodItem.Create(Guid.NewGuid(), revision.Id, lunch.Id, Chicken.Id, 150, 0, null);
        var group = DietReplacementGroup.Create(Guid.NewGuid(), revision.Id, variant.Id, lunch.Id, chickenRow.Id, "Protein", 0, ReplacementMode.Flexible);
        var calculated = DietReplacementOption.Create(Guid.NewGuid(), revision.Id, group.Id, Turkey.Id, null, null, 0, null);
        var overridden = DietReplacementOption.Create(Guid.NewGuid(), revision.Id, group.Id, Turkey.Id, null, 120, 1, null);
        var aggregate = new DietPlanAggregate(plan, revision, 1, [], [variant], [breakfast, lunch],
            [riceRow, potatoRow, chickenRow], [group], [calculated, overridden], [optionA, optionB]);
        var foods = new[] { Rice, Potato, Chicken, Turkey }.ToDictionary(x => x.Id);

        var figures = new DietPlanFigures(aggregate, foods, FoodReplacementRule.DominantMacro);

        Assert.Equal(130m, figures.Meal(breakfast.Id).Calories);
        Assert.Equal(77m, figures.Meal(breakfast.Id, optionB.Id).Calories);
        Assert.Equal(optionA.Id, figures.OptionFor(breakfast.Id, Guid.NewGuid()));
        Assert.Equal(potatoRow.Id, Assert.Single(figures.RowsOf(breakfast.Id, optionB.Id)).Id);
        Assert.Null(figures.OptionFor(lunch.Id));
        Assert.Equal(chickenRow.Id, Assert.Single(figures.RowsOf(lunch.Id)).Id);
        Assert.Equal(((decimal?)155m, true), figures.Quantity(calculated));
        Assert.Equal(((decimal?)120m, false), figures.Quantity(overridden));
        Assert.Equal(209.25m, figures.Alternative(calculated).Calories);
    }

    private static FoodItem Food(string name, decimal calories, decimal protein, decimal carbohydrates, decimal fat) =>
        FoodItem.Create(null, new FoodValues(name, null, Guid.NewGuid(), FoodBaseUnit.Gram, 100, calories, protein, carbohydrates, fat));
}
