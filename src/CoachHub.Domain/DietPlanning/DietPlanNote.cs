using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class DietPlanNote : Entity
{
    private DietPlanNote() { }
    public Guid DietPlanId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public static DietPlanNote Create(Guid id, Guid planId, string text, int order, bool isActive) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        DietPlanId = DietPlanText.RequiredId(planId, nameof(planId)),
        Text = DietPlanText.Required(text, 2000, nameof(text)),
        Order = DietPlanText.Order(order, nameof(order)),
        IsActive = isActive
    };
    public void SetActive(bool isActive) => IsActive = isActive;
}