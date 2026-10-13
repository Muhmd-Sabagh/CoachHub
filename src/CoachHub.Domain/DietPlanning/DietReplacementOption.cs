using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>
/// One permitted alternative: a food (with its quantity in <see cref="FoodUnitId"/> or the base unit) or a
/// whole meal. A food's quantity may be left out only in a <see cref="ReplacementMode.Flexible"/> group,
/// where it is calculated; the service checks that, since it depends on the group.
/// </summary>
public sealed class DietReplacementOption : Entity, IPlanRevisionContent
{
    private DietReplacementOption() { }
    public Guid RevisionId { get; private set; }
    public Guid DietReplacementGroupId { get; private set; }
    public Guid? ReplacementFoodItemId { get; private set; }
    public Guid? ReplacementMealId { get; private set; }
    public decimal? Quantity { get; private set; }
    /// <summary>The unit of <see cref="Quantity"/> (and of a calculated quantity): one of the food's units, or null for its base unit.</summary>
    public Guid? FoodUnitId { get; private set; }
    public int Order { get; private set; }
    public string? Notes { get; private set; }

    public static DietReplacementOption Create(
        Guid id, Guid revisionId, Guid groupId, Guid? foodItemId, Guid? mealId,
        decimal? quantity, int order, string? notes, Guid? foodUnitId = null)
    {
        if (foodItemId.HasValue == mealId.HasValue)
            throw new ArgumentException("Choose exactly one replacement food or meal.");
        if (foodItemId == Guid.Empty || mealId == Guid.Empty || foodUnitId == Guid.Empty)
            throw new ArgumentException("Replacement identifiers cannot be empty.");
        if (mealId.HasValue && (quantity.HasValue || foodUnitId.HasValue))
            throw new ArgumentException("Meal replacements do not use a quantity.", nameof(quantity));
        return new DietReplacementOption
        {
            Id = DietPlanText.RequiredId(id, nameof(id)),
            RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
            DietReplacementGroupId = DietPlanText.RequiredId(groupId, nameof(groupId)),
            ReplacementFoodItemId = foodItemId,
            ReplacementMealId = mealId,
            Quantity = quantity.HasValue ? DietPlanText.Quantity(quantity.Value, nameof(quantity)) : null,
            FoodUnitId = foodUnitId,
            Order = DietPlanText.Order(order, nameof(order)),
            Notes = DietPlanText.Optional(notes, 1000, nameof(notes))
        };
    }
}
