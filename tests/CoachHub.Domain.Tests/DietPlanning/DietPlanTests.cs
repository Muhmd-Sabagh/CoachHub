using CoachHub.Domain.DietPlanning;

namespace CoachHub.Domain.Tests.DietPlanning;

public sealed class DietPlanTests
{
    [Fact]
    public void Plan_and_nested_entities_normalize_bilingual_content_and_order()
    {
        var plan = DietPlan.Create(Guid.NewGuid(), "  Cutting plan ", null, null, DateTimeOffset.UtcNow);
        var revisionId = Guid.NewGuid();
        var variant = DietPlanVariant.Create(Guid.NewGuid(), revisionId, " High carb ", " ", 1, true, null);
        var meal = Meal.Create(Guid.NewGuid(), revisionId, variant.Id, " Breakfast ", " إفطار ", 0, null);

        Assert.Equal("Cutting plan", plan.NameEn);
        Assert.Null(variant.NameAr);
        Assert.Equal(revisionId, meal.RevisionId);
        Assert.Equal("Breakfast", meal.NameEn);
        Assert.Equal("إفطار", meal.NameAr);
    }

    [Fact]
    public void Replacement_option_requires_exactly_one_structured_target()
    {
        Assert.Throws<ArgumentException>(() => DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 0, null));
        Assert.Throws<ArgumentException>(() => DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null, 0, null));

        var food = DietReplacementOption.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 75, 0, "swap");
        Assert.Equal(75, food.Quantity);
        Assert.Null(food.ReplacementMealId);
    }

    [Fact]
    public void A_row_starts_its_own_lineage_unless_it_continues_one()
    {
        var revisionId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var earlier = Guid.NewGuid();
        Assert.Equal(id, Meal.Create(id, revisionId, Guid.NewGuid(), "Breakfast", null, 1, null).LineageId);
        Assert.Equal(earlier, Meal.Create(id, revisionId, Guid.NewGuid(), "Breakfast", null, 1, null, lineageId: earlier).LineageId);
        Assert.Equal(earlier, MealOption.Create(id, revisionId, Guid.NewGuid(), "Option A", null, 1, earlier).LineageId);
        Assert.Equal(id, DietPlanVariant.Create(id, revisionId, "Training day", null, 1, true, null).LineageId);
    }
}
