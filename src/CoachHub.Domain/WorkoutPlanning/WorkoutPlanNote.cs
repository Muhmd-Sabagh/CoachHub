using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

/// <summary>A note of one workout plan version; notes are versioned with the content.</summary>
public sealed class WorkoutPlanNote : Entity, IPlanRevisionContent
{
    private WorkoutPlanNote() { }
    public Guid RevisionId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public static WorkoutPlanNote Create(Guid id, Guid revisionId, string text, int order, bool isActive) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), RevisionId = WorkoutText.Id(revisionId, nameof(revisionId)),
        Text = WorkoutText.Required(text, 2000, nameof(text)), Order = WorkoutText.Order(order, nameof(order)),
        IsActive = isActive
    };
    public void SetActive(bool isActive) => IsActive = isActive;
}
