using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

public sealed class WorkoutExercise : Entity
{
    private WorkoutExercise() { }
    public Guid WorkoutDayId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Order { get; private set; }
    public string? Sets { get; private set; }
    public string? Repetitions { get; private set; }
    public string? Rest { get; private set; }
    public string? Tempo { get; private set; }
    public string? RpeRir { get; private set; }
    public string? Notes { get; private set; }

    public static WorkoutExercise Create(
        Guid id, Guid dayId, Guid exerciseId, int order, string? sets, string? repetitions,
        string? rest, string? tempo, string? rpeRir, string? notes) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), WorkoutDayId = WorkoutText.Id(dayId, nameof(dayId)),
        ExerciseId = WorkoutText.Id(exerciseId, nameof(exerciseId)), Order = WorkoutText.Order(order, nameof(order)),
        Sets = WorkoutText.Optional(sets, 50, nameof(sets)),
        Repetitions = WorkoutText.Optional(repetitions, 50, nameof(repetitions)),
        Rest = WorkoutText.Optional(rest, 50, nameof(rest)), Tempo = WorkoutText.Optional(tempo, 50, nameof(tempo)),
        RpeRir = WorkoutText.Optional(rpeRir, 50, nameof(rpeRir)), Notes = WorkoutText.Optional(notes, 1000, nameof(notes))
    };
}
