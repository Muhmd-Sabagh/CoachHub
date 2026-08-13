using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

public sealed class WorkoutPlanNote : Entity
{
    private WorkoutPlanNote() { }
    public Guid WorkoutPlanId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public static WorkoutPlanNote Create(Guid id, Guid planId, string text, int order, bool isActive) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), WorkoutPlanId = WorkoutText.Id(planId, nameof(planId)),
        Text = WorkoutText.Required(text, 2000, nameof(text)), Order = WorkoutText.Order(order, nameof(order)),
        IsActive = isActive
    };
    public void SetActive(bool isActive) => IsActive = isActive;
}
