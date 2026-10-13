using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>How the quantity of a food alternative is decided (§7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReplacementMode>))]
public enum ReplacementMode
{
    /// <summary>The client gets the quantity the coach set for each alternative.</summary>
    Exact,

    /// <summary>
    /// The quantity is calculated to match the original food (by calories or its dominant macro, per the
    /// coach's replacement rule). A quantity the coach sets overrides the calculation.
    /// </summary>
    Flexible
}

/// <summary>
/// The alternatives the coach permits for a food row (§7), or for a whole meal when no row is targeted.
/// The client's "Change Food" offers only these.
/// </summary>
public sealed class DietReplacementGroup : Entity, IPlanRevisionContent
{
    private DietReplacementGroup() { }
    public Guid RevisionId { get; private set; }
    public Guid DietPlanVariantId { get; private set; }
    public Guid TargetMealId { get; private set; }
    public Guid? TargetMealFoodItemId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public ReplacementMode Mode { get; private set; }

    public static DietReplacementGroup Create(
        Guid id, Guid revisionId, Guid variantId, Guid targetMealId, Guid? targetFoodItemId,
        string title, int order, ReplacementMode mode = ReplacementMode.Exact) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        DietPlanVariantId = DietPlanText.RequiredId(variantId, nameof(variantId)),
        TargetMealId = DietPlanText.RequiredId(targetMealId, nameof(targetMealId)),
        TargetMealFoodItemId = targetFoodItemId == Guid.Empty
            ? throw new ArgumentException("Target food identifier cannot be empty.", nameof(targetFoodItemId))
            : targetFoodItemId,
        Title = DietPlanText.Required(title, 300, nameof(title)),
        Order = DietPlanText.Order(order, nameof(order)),
        Mode = Enum.IsDefined(mode) ? mode : throw new ArgumentException("Unknown replacement mode.", nameof(mode))
    };
}
