using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;

namespace CoachHub.Domain.Tests.FoodTracking;

public sealed class FoodLogEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly Guid Coach = Guid.NewGuid(), Client = Guid.NewGuid(), Food = Guid.NewGuid();

    private static FoodLogPortion Portion(decimal quantity = 2, Guid? unit = null) => new(Food, unit, quantity, quantity * 50, "egg", "بيضة");
    private static readonly FoodLogNutrients Nutrients = new(140, 12, 1, 10, 0);

    [Fact]
    public void The_source_follows_the_planned_reference()
    {
        Guid meal = Guid.NewGuid(), row = Guid.NewGuid(), option = Guid.NewGuid();
        var extra = FoodLogEntry.ForFood(Coach, Client, Today, MealType.Snack, "Eggs", null, Portion(), Nutrients, null, Now);
        var planned = FoodLogEntry.ForFood(Coach, Client, Today, MealType.Breakfast, "Eggs", "بيض", Portion(), Nutrients,
            new PlannedFoodReference(meal, row), Now);
        var swap = FoodLogEntry.ForFood(Coach, Client, Today, MealType.Breakfast, "Eggs", null, Portion(), Nutrients,
            new PlannedFoodReference(meal, row, option), Now);

        Assert.Equal((FoodLogSource.Extra, (Guid?)null), (extra.Source, extra.PlannedMealId));
        Assert.Equal((FoodLogSource.Planned, (Guid?)meal, (Guid?)row, (Guid?)null),
            (planned.Source, planned.PlannedMealId, planned.PlannedMealFoodItemId, planned.ReplacementOptionId));
        Assert.Equal((FoodLogSource.Swap, (Guid?)option), (swap.Source, swap.ReplacementOptionId));
        Assert.Equal((2m, 100m, "egg", "بيضة", 140m), (planned.Quantity!.Value, planned.BaseAmount!.Value, planned.UnitLabel, planned.UnitLabelAr, planned.Calories));
        Assert.False(planned.IsCustom);
    }

    [Fact]
    public void A_custom_entry_has_nutrients_but_no_food()
    {
        var entry = FoodLogEntry.Custom(Coach, Client, Today, MealType.Dinner, "  Restaurant burger ", new(850, 40, 60, 45, 3), Now);

        Assert.Equal((true, FoodLogSource.Extra, "Restaurant burger", (decimal?)null), (entry.IsCustom, entry.Source, entry.NameEn, entry.Quantity));
        entry.ChangeCustom("Burger", new(900, 42, 62, 48), Now.AddMinutes(5));
        Assert.Equal((900m, Now.AddMinutes(5)), (entry.Calories, entry.UpdatedAt));
        Assert.Throws<InvalidOperationException>(() => entry.ChangePortion(Portion(), Nutrients, Now));
    }

    [Fact]
    public void A_food_entry_changes_its_portion_but_keeps_its_food()
    {
        var entry = FoodLogEntry.ForFood(Coach, Client, Today, MealType.Lunch, "Eggs", null, Portion(), Nutrients, null, Now);

        entry.ChangePortion(Portion(3), new(210, 18, 1.5m, 15), Now);
        entry.MoveTo(MealType.Snack, Now);

        Assert.Equal((3m, 210m, MealType.Snack), (entry.Quantity!.Value, entry.Calories, entry.Slot));
        Assert.Throws<ArgumentException>(() => entry.ChangePortion(Portion() with { FoodItemId = Guid.NewGuid() }, Nutrients, Now));
        Assert.Throws<InvalidOperationException>(() => entry.ChangeCustom("x", Nutrients, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Quantities_are_positive_and_bounded(decimal quantity) =>
        Assert.Throws<ArgumentException>(() =>
            FoodLogEntry.ForFood(Coach, Client, Today, MealType.Lunch, "Eggs", null, Portion(quantity), Nutrients, null, Now));

    [Fact]
    public void Invalid_entries_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => FoodLogEntry.Custom(Coach, Client, Today, MealType.Lunch, "x", new(-1, 0, 0, 0), Now));
        Assert.Throws<ArgumentException>(() => FoodLogEntry.Custom(Coach, Client, Today, MealType.Lunch, " ", new(1, 0, 0, 0), Now));
        Assert.Throws<ArgumentException>(() => FoodLogEntry.Custom(Coach, Client, Today, (MealType)9, "x", new(1, 0, 0, 0), Now));
        Assert.Throws<ArgumentException>(() => FoodLogEntry.Custom(Guid.Empty, Client, Today, MealType.Lunch, "x", new(1, 0, 0, 0), Now));
        Assert.Throws<ArgumentException>(() => FoodLogEntry.Custom(Coach, Client, Today, MealType.Lunch, new string('a', 256), new(1, 0, 0, 0), Now));
        Assert.Throws<ArgumentException>(() =>
            FoodLogEntry.ForFood(Coach, Client, Today, MealType.Lunch, "Eggs", null, Portion() with { UnitLabel = " " }, Nutrients, null, Now));
    }
}

public sealed class NutritionDayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Choices_are_set_changed_and_cleared_per_meal()
    {
        var day = NutritionDay.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 13), Now);
        Guid variant = Guid.NewGuid(), breakfast = Guid.NewGuid(), optionA = Guid.NewGuid(), optionB = Guid.NewGuid();

        day.ChooseVariant(variant, Now);
        day.ChooseOption(breakfast, optionA, Now);
        day.ChooseOption(breakfast, optionB, Now);
        Assert.Equal((variant, optionB), (day.VariantId!.Value, day.OptionFor(breakfast)!.Value));
        Assert.Single(day.MealChoices);

        day.ChooseOption(breakfast, null, Now);
        Assert.Null(day.OptionFor(breakfast));
        Assert.Empty(day.MealChoices);
        Assert.Throws<ArgumentException>(() => day.ChooseVariant(Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => day.ChooseOption(breakfast, Guid.Empty, Now));
    }
}

public sealed class FlexibleMealTests
{
    [Fact]
    public void Meal_options_and_alternatives_validate_their_shape()
    {
        Guid revision = Guid.NewGuid(), meal = Guid.NewGuid(), group = Guid.NewGuid();
        var option = MealOption.Create(Guid.NewGuid(), revision, meal, " Option A ", "الخيار أ", 0);
        Assert.Equal(("Option A", "الخيار أ"), (option.NameEn, option.NameAr));
        Assert.Throws<ArgumentException>(() => MealOption.Create(Guid.NewGuid(), revision, meal, " ", null, 0));

        var row = MealFoodItem.Create(Guid.NewGuid(), revision, meal, Guid.NewGuid(), 2, 0, null, mealOptionId: option.Id);
        Assert.Equal(option.Id, row.MealOptionId);
        Assert.Throws<ArgumentException>(() => MealFoodItem.Create(Guid.NewGuid(), revision, meal, Guid.NewGuid(), 2, 0, null, mealOptionId: Guid.Empty));

        // A flexible food alternative may leave its quantity to the calculation; a meal alternative has no quantity or unit.
        var calculated = DietReplacementOption.Create(Guid.NewGuid(), revision, group, Guid.NewGuid(), null, null, 0, null);
        Assert.Null(calculated.Quantity);
        Assert.Throws<ArgumentException>(() =>
            DietReplacementOption.Create(Guid.NewGuid(), revision, group, null, meal, null, 0, null, Guid.NewGuid()));
        Assert.Equal(ReplacementMode.Exact,
            DietReplacementGroup.Create(Guid.NewGuid(), revision, Guid.NewGuid(), meal, null, "Alternatives", 0).Mode);
        Assert.Throws<ArgumentException>(() =>
            DietReplacementGroup.Create(Guid.NewGuid(), revision, Guid.NewGuid(), meal, null, "Alternatives", 0, (ReplacementMode)5));
    }
}
