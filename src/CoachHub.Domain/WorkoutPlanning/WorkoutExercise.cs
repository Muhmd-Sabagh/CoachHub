using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

/// <summary>The part of a workout an exercise belongs to (§16): warm-up first, cool-down last.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutBlock>))]
public enum WorkoutBlock
{
    WarmUp,
    Main,
    Cooldown
}

/// <summary>
/// An exercise of a training day (§16). Its prescription is typed (D3): one <see cref="PrescribedSet"/> per set.
/// Exercises done back to back share a <see cref="WorkoutSetGroup"/> (superset or giant set).
/// </summary>
public sealed class WorkoutExercise : Entity, IPlanRevisionContent
{
    public const int MaximumSets = 20;

    private WorkoutExercise() { }
    public Guid RevisionId { get; private set; }
    public Guid WorkoutDayId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Order { get; private set; }
    public WorkoutBlock Block { get; private set; }
    public Guid? SetGroupId { get; private set; }
    public string? Notes { get; private set; }

    public static WorkoutExercise Create(
        Guid id, Guid revisionId, Guid dayId, Guid exerciseId, int order, string? notes,
        WorkoutBlock block = WorkoutBlock.Main, Guid? setGroupId = null) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), RevisionId = WorkoutText.Id(revisionId, nameof(revisionId)),
        WorkoutDayId = WorkoutText.Id(dayId, nameof(dayId)),
        ExerciseId = WorkoutText.Id(exerciseId, nameof(exerciseId)), Order = WorkoutText.Order(order, nameof(order)),
        Block = Enum.IsDefined(block) ? block : throw new ArgumentException("Unknown workout block.", nameof(block)),
        SetGroupId = setGroupId == Guid.Empty ? throw new ArgumentException("Set group identifier cannot be empty.", nameof(setGroupId)) : setGroupId,
        Notes = WorkoutText.Optional(notes, 1000, nameof(notes))
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<SetGroupType>))]
public enum SetGroupType
{
    /// <summary>Two exercises done back to back.</summary>
    Superset,

    /// <summary>Three or more exercises done back to back.</summary>
    GiantSet
}

/// <summary>Exercises of a day done back to back (§16), with the rest after each round.</summary>
public sealed class WorkoutSetGroup : Entity, IPlanRevisionContent
{
    public const int MaximumExercises = 10;

    private WorkoutSetGroup() { }
    public Guid RevisionId { get; private set; }
    public Guid WorkoutDayId { get; private set; }
    public SetGroupType Type { get; private set; }
    public int? RestSeconds { get; private set; }
    public string? Notes { get; private set; }

    public static WorkoutSetGroup Create(Guid id, Guid revisionId, Guid dayId, SetGroupType type, int? restSeconds, string? notes) => new()
    {
        Id = WorkoutText.Id(id, nameof(id)), RevisionId = WorkoutText.Id(revisionId, nameof(revisionId)),
        WorkoutDayId = WorkoutText.Id(dayId, nameof(dayId)),
        Type = Enum.IsDefined(type) ? type : throw new ArgumentException("Unknown set group type.", nameof(type)),
        RestSeconds = Prescription.Range(restSeconds, 0, PrescribedSet.MaximumRestSeconds, nameof(restSeconds)),
        Notes = WorkoutText.Optional(notes, 500, nameof(notes))
    };

    /// <summary>How many exercises the group type takes.</summary>
    public static bool Fits(SetGroupType type, int exercises) => type switch
    {
        SetGroupType.Superset => exercises == 2,
        _ => exercises is >= 3 and <= MaximumExercises
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<SetType>))]
public enum SetType
{
    Normal,
    WarmUp,
    /// <summary>A drop set: straight after the previous set, at a lower load.</summary>
    Drop
}

/// <summary>
/// A tempo in seconds per phase (§16): eccentric, pause at the bottom, concentric, pause at the top; "3-1-1-0".
/// </summary>
public sealed record Tempo(int Eccentric, int BottomPause, int Concentric, int TopPause)
{
    public const int MaximumSeconds = 20;

    public Tempo Validate()
    {
        int[] phases = [Eccentric, BottomPause, Concentric, TopPause];
        if (phases.Any(x => x is < 0 or > MaximumSeconds))
            throw new ArgumentException($"Each tempo phase is 0–{MaximumSeconds} seconds.", "tempo");
        return this;
    }

    public override string ToString() => $"{Eccentric}-{BottomPause}-{Concentric}-{TopPause}";
}

/// <summary>Everything prescribed for one set (D3): every value is optional, but typed.</summary>
public sealed record SetPrescription(
    SetType Type = SetType.Normal,
    int? RepsMin = null,
    int? RepsMax = null,
    decimal? LoadKg = null,
    int? Rir = null,
    decimal? Rpe = null,
    Tempo? Tempo = null,
    int? RestSeconds = null,
    int? DurationSeconds = null,
    bool IsSkippable = false,
    string? Notes = null);

/// <summary>
/// One prescribed set of a workout exercise (§16): reps (exact, or a range), load in kg, RIR, RPE, tempo, rest,
/// duration, whether the client may skip it, and notes. A drop set follows another set.
/// </summary>
public sealed class PrescribedSet : Entity, IPlanRevisionContent
{
    public const int MaximumReps = 1000;
    public const decimal MaximumLoadKg = 1000m;
    public const int MaximumRestSeconds = 3600;
    public const int MaximumDurationSeconds = 36000;

    private PrescribedSet() { }
    public Guid RevisionId { get; private set; }
    public Guid WorkoutExerciseId { get; private set; }
    /// <summary>The set's position: 0 is set 1.</summary>
    public int Order { get; private set; }
    public SetType Type { get; private set; }
    public int? RepsMin { get; private set; }
    public int? RepsMax { get; private set; }
    public decimal? LoadKg { get; private set; }
    public int? Rir { get; private set; }
    public decimal? Rpe { get; private set; }
    public int? TempoEccentric { get; private set; }
    public int? TempoBottomPause { get; private set; }
    public int? TempoConcentric { get; private set; }
    public int? TempoTopPause { get; private set; }
    public int? RestSeconds { get; private set; }
    public int? DurationSeconds { get; private set; }
    public bool IsSkippable { get; private set; }
    public string? Notes { get; private set; }

    public Tempo? Tempo => TempoEccentric is { } eccentric
        ? new Tempo(eccentric, TempoBottomPause!.Value, TempoConcentric!.Value, TempoTopPause!.Value)
        : null;

    public SetPrescription Prescription => new(Type, RepsMin, RepsMax, LoadKg, Rir, Rpe, Tempo, RestSeconds, DurationSeconds, IsSkippable, Notes);

    public static PrescribedSet Create(Guid id, Guid revisionId, Guid workoutExerciseId, int order, SetPrescription values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!Enum.IsDefined(values.Type)) throw new ArgumentException("Unknown set type.", "type");
        if (order == 0 && values.Type == SetType.Drop) throw new ArgumentException("A drop set follows another set.", "type");
        if (values.RepsMax.HasValue && !values.RepsMin.HasValue) throw new ArgumentException("Give the minimum reps too.", "repsMin");
        if (values.RepsMin > values.RepsMax) throw new ArgumentException("The minimum reps cannot exceed the maximum.", "repsMax");
        if (values.Rpe is { } rpe && (rpe < 1 || rpe > 10 || rpe * 2 != decimal.Truncate(rpe * 2)))
            throw new ArgumentException("RPE is 1–10 in steps of 0.5.", "rpe");
        if (values.LoadKg is { } load && (load < 0 || load > MaximumLoadKg || decimal.Round(load, 2) != load))
            throw new ArgumentException($"Load is 0–{MaximumLoadKg} kg with at most two decimals.", "loadKg");
        var tempo = values.Tempo?.Validate();
        return new PrescribedSet
        {
            Id = WorkoutText.Id(id, nameof(id)),
            RevisionId = WorkoutText.Id(revisionId, nameof(revisionId)),
            WorkoutExerciseId = WorkoutText.Id(workoutExerciseId, nameof(workoutExerciseId)),
            Order = WorkoutText.Order(order, nameof(order)),
            Type = values.Type,
            RepsMin = WorkoutPlanning.Prescription.Range(values.RepsMin, 0, MaximumReps, "repsMin"),
            RepsMax = WorkoutPlanning.Prescription.Range(values.RepsMax, 0, MaximumReps, "repsMax"),
            LoadKg = values.LoadKg,
            Rir = WorkoutPlanning.Prescription.Range(values.Rir, 0, 10, "rir"),
            Rpe = values.Rpe,
            TempoEccentric = tempo?.Eccentric,
            TempoBottomPause = tempo?.BottomPause,
            TempoConcentric = tempo?.Concentric,
            TempoTopPause = tempo?.TopPause,
            RestSeconds = WorkoutPlanning.Prescription.Range(values.RestSeconds, 0, MaximumRestSeconds, "restSeconds"),
            DurationSeconds = WorkoutPlanning.Prescription.Range(values.DurationSeconds, 1, MaximumDurationSeconds, "durationSeconds"),
            IsSkippable = values.IsSkippable,
            Notes = WorkoutText.Optional(values.Notes, 500, "notes")
        };
    }
}

internal static class Prescription
{
    public static int? Range(int? value, int minimum, int maximum, string name) =>
        value is { } number && (number < minimum || number > maximum)
            ? throw new ArgumentException($"Must be between {minimum} and {maximum}.", name)
            : value;
}
