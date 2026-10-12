using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class DietReplacementGroup : Entity, IPlanRevisionContent
{
    private DietReplacementGroup() { }
    public Guid RevisionId { get; private set; }
    public Guid DietPlanVariantId { get; private set; }
    public Guid TargetMealId { get; private set; }
    public Guid? TargetMealFoodItemId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }

    public static DietReplacementGroup Create(
        Guid id, Guid revisionId, Guid variantId, Guid targetMealId, Guid? targetFoodItemId,
        string title, int order) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        DietPlanVariantId = DietPlanText.RequiredId(variantId, nameof(variantId)),
        TargetMealId = DietPlanText.RequiredId(targetMealId, nameof(targetMealId)),
        TargetMealFoodItemId = targetFoodItemId == Guid.Empty
            ? throw new ArgumentException("Target food identifier cannot be empty.", nameof(targetFoodItemId))
            : targetFoodItemId,
        Title = DietPlanText.Required(title, 300, nameof(title)),
        Order = DietPlanText.Order(order, nameof(order))
    };
}
