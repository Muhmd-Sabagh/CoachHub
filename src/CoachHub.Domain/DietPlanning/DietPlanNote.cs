using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>A note of one diet plan version; notes are versioned with the content.</summary>
public sealed class DietPlanNote : Entity, IPlanRevisionContent
{
    private DietPlanNote() { }
    public Guid RevisionId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public static DietPlanNote Create(Guid id, Guid revisionId, string text, int order, bool isActive) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        Text = DietPlanText.Required(text, 2000, nameof(text)),
        Order = DietPlanText.Order(order, nameof(order)),
        IsActive = isActive
    };
    public void SetActive(bool isActive) => IsActive = isActive;
}
