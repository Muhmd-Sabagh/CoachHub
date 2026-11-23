using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>
/// One option of a flexible meal (§13), for example "Option A: Eggs + Oats". A meal with options has
/// every food row in one of them; the client eats one option per day. The first option (by order) is the
/// meal's default, used for the plan's totals.
/// </summary>
public sealed class MealOption : Entity, IPlanRevisionContent
{
    public const int MaximumPerMeal = 10;

    private MealOption() { }
    public Guid RevisionId { get; private set; }
    public Guid MealId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Order { get; private set; }

    /// <summary>
    /// The same row across plan versions (P22): a new version's row keeps the lineage of the row it came from, so a
    /// client's day choices and logged meals still match after a mid-day new version. A new row starts its own.
    /// </summary>
    public Guid LineageId { get; private set; }

    public static MealOption Create(
        Guid id, Guid revisionId, Guid mealId, string nameEn, string? nameAr, int order, Guid? lineageId = null) => new()
    {
        LineageId = lineageId ?? id,
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        MealId = DietPlanText.RequiredId(mealId, nameof(mealId)),
        NameEn = DietPlanText.Required(nameEn, 100, nameof(nameEn)),
        NameAr = DietPlanText.Optional(nameAr, 100, nameof(nameAr)),
        Order = DietPlanText.Order(order, nameof(order))
    };
}
