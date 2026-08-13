using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class DietReplacementOption : Entity
{
    private DietReplacementOption() { }
    public Guid DietReplacementGroupId { get; private set; }
    public Guid? ReplacementFoodItemId { get; private set; }
    public Guid? ReplacementMealId { get; private set; }
    public decimal? Quantity { get; private set; }
    public int Order { get; private set; }
    public string? Notes { get; private set; }

    public static DietReplacementOption Create(
        Guid id, Guid groupId, Guid? foodItemId, Guid? mealId,
        decimal? quantity, int order, string? notes)
    {
        if (foodItemId.HasValue == mealId.HasValue)
            throw new ArgumentException("Choose exactly one replacement food or meal.");
        if (foodItemId == Guid.Empty || mealId == Guid.Empty)
            throw new ArgumentException("Replacement identifiers cannot be empty.");
        if (foodItemId.HasValue && !quantity.HasValue)
            throw new ArgumentException("Food replacements require a quantity.", nameof(quantity));
        if (mealId.HasValue && quantity.HasValue)
            throw new ArgumentException("Meal replacements do not use a quantity.", nameof(quantity));
        return new DietReplacementOption
        {
            Id = DietPlanText.RequiredId(id, nameof(id)),
            DietReplacementGroupId = DietPlanText.RequiredId(groupId, nameof(groupId)),
            ReplacementFoodItemId = foodItemId,
            ReplacementMealId = mealId,
            Quantity = quantity.HasValue ? DietPlanText.Quantity(quantity.Value, nameof(quantity)) : null,
            Order = DietPlanText.Order(order, nameof(order)),
            Notes = DietPlanText.Optional(notes, 1000, nameof(notes))
        };
    }
}