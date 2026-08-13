using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class DietPlanVersion : Entity
{
    private DietPlanVersion() { }
    public Guid DietPlanId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Order { get; private set; }
    public bool IsActiveForPdf { get; private set; }
    public string? Notes { get; private set; }

    public static DietPlanVersion Create(
        Guid id, Guid planId, string nameEn, string? nameAr,
        int order, bool isActiveForPdf, string? notes) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        DietPlanId = DietPlanText.RequiredId(planId, nameof(planId)),
        NameEn = DietPlanText.Required(nameEn, 200, nameof(nameEn)),
        NameAr = DietPlanText.Optional(nameAr, 200, nameof(nameAr)),
        Order = DietPlanText.Order(order, nameof(order)),
        IsActiveForPdf = isActiveForPdf,
        Notes = DietPlanText.Optional(notes, 2000, nameof(notes))
    };
}