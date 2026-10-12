using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class Meal : Entity, IPlanRevisionContent
{
    private Meal() { }
    public Guid RevisionId { get; private set; }
    public Guid DietPlanVariantId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Order { get; private set; }
    public string? Notes { get; private set; }

    public static Meal Create(
        Guid id, Guid revisionId, Guid variantId, string nameEn, string? nameAr, int order, string? notes) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        DietPlanVariantId = DietPlanText.RequiredId(variantId, nameof(variantId)),
        NameEn = DietPlanText.Required(nameEn, 200, nameof(nameEn)),
        NameAr = DietPlanText.Optional(nameAr, 200, nameof(nameAr)),
        Order = DietPlanText.Order(order, nameof(order)),
        Notes = DietPlanText.Optional(notes, 2000, nameof(notes))
    };
}
