using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

public sealed class WorkoutDay : Entity
{
    private WorkoutDay() { }
    public Guid WorkoutPlanId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public string? Subtitle { get; private set; }
    public string? Notes { get; private set; }
    public int Order { get; private set; }

    public static WorkoutDay Create(
        Guid id, Guid planId, string nameEn, string? nameAr, string? subtitle, string? notes, int order) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), WorkoutPlanId = WorkoutText.Id(planId, nameof(planId)),
        NameEn = WorkoutText.Required(nameEn, 100, nameof(nameEn)),
        NameAr = WorkoutText.Optional(nameAr, 100, nameof(nameAr)),
        Subtitle = WorkoutText.Optional(subtitle, 100, nameof(subtitle)),
        Notes = WorkoutText.Optional(notes, 1000, nameof(notes)), Order = WorkoutText.Order(order, nameof(order))
    };
}
