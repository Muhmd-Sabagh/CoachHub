using CoachHub.Domain.DietPlanning;

namespace CoachHub.Domain.Tests.DietPlanning;

public sealed class DietPlanTests
{
    [Fact]
    public void Plan_and_nested_entities_normalize_bilingual_content_and_order()
    {
        var plan = DietPlan.Create("  Cutting plan ", null, null, DateTimeOffset.UtcNow);
        var version = DietPlanVersion.Create(Guid.NewGuid(), plan.Id, " High carb ", " ", 1, true, null);
        var meal = Meal.Create(Guid.NewGuid(), version.Id, " Breakfast ", " إفطار ", 0, null);

        Assert.Equal("Cutting plan", plan.NameEn);
        Assert.Null(version.NameAr);
        Assert.Equal("Breakfast", meal.NameEn);
        Assert.Equal("إفطار", meal.NameAr);
    }

    [Fact]
    public void Replacement_option_requires_exactly_one_structured_target()
    {
        Assert.Throws<ArgumentException>(() => DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 0, null));
        Assert.Throws<ArgumentException>(() => DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, null, null, 0, null));

        var food = DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 75, 0, "swap");
        Assert.Equal(75, food.Quantity);
        Assert.Null(food.ReplacementMealId);
    }
}
