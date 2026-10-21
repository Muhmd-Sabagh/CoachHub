using System.Text.Json.Serialization;
using CoachHub.Domain.Common;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.WorkoutTracking;

/// <summary>Whether a logged workout is still going on or finished (§18).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutSessionStatus>))]
public enum WorkoutSessionStatus
{
    InProgress,
    Completed
}

/// <summary>Whether a set was done or skipped (§18: "skip if permitted").</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoggedSetStatus>))]
public enum LoggedSetStatus
{
    Completed,
    Skipped
}

/// <summary>
/// The planned workout day a session follows: the plan, its version and the day. The identifiers point at plan
/// version rows, which may later be archived or deleted, so they are references only; the session stands on its own.
/// <paramref name="DayOrder"/> is the day's place in the rotation, which survives a new version's fresh row IDs.
/// </summary>
public sealed record PlannedWorkoutReference(Guid PlanId, Guid RevisionId, int VersionNumber, Guid WorkoutDayId, int DayOrder);

/// <summary>
/// One workout the client did or is doing (§18, D3): the date, the planned day it follows (if any), and per exercise
/// the sets as performed. The client writes it; the coach reads it. Edits stay possible after completion, so the
/// history can be corrected.
/// </summary>
public sealed class WorkoutSession : Entity, ITenantOwned
{
    public const int MaximumExercises = 100;
    public const int MaximumNotesLength = 2000;

    private readonly List<LoggedExercise> _exercises = [];

    private WorkoutSession() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public WorkoutSessionStatus Status { get; private set; }

    /// <summary>The day's name when it started (the planned day's, or the client's for a free workout).</summary>
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    public Guid? PlanId { get; private set; }
    public Guid? RevisionId { get; private set; }
    public int? VersionNumber { get; private set; }
    public Guid? WorkoutDayId { get; private set; }
    public int? DayOrder { get; private set; }

    public string? Notes { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<LoggedExercise> Exercises => _exercises;
    public bool IsCompleted => Status == WorkoutSessionStatus.Completed;

    /// <summary>Starts a workout: a planned day when <paramref name="planned"/> is given, otherwise a free workout.</summary>
    public static WorkoutSession Start(
        Guid coachId, Guid clientId, DateOnly date, string nameEn, string? nameAr,
        PlannedWorkoutReference? planned, DateTimeOffset now)
    {
        var session = new WorkoutSession
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = TrackingText.Id(clientId, nameof(clientId)),
            Date = date,
            Status = WorkoutSessionStatus.InProgress,
            NameEn = TrackingText.Required(nameEn, 100, nameof(nameEn)),
            NameAr = TrackingText.Optional(nameAr, 100, nameof(nameAr)),
            StartedAt = now,
            UpdatedAt = now
        };
        if (planned is not null)
        {
            if (planned.VersionNumber < 1) throw new ArgumentOutOfRangeException(nameof(planned));
            session.PlanId = TrackingText.Id(planned.PlanId, "planId");
            session.RevisionId = TrackingText.Id(planned.RevisionId, "revisionId");
            session.VersionNumber = planned.VersionNumber;
            session.WorkoutDayId = TrackingText.Id(planned.WorkoutDayId, "workoutDayId");
            session.DayOrder = TrackingText.Order(planned.DayOrder, "dayOrder");
        }
        return session;
    }

    public bool IsPlanned => WorkoutDayId.HasValue;

    /// <summary>
    /// Adds an exercise at the end: a planned one (<paramref name="plannedExerciseId"/>) or one the client adds.
    /// </summary>
    public LoggedExercise AddExercise(
        Guid exerciseId, Guid? plannedExerciseId, WorkoutBlock block, string? groupLabel, DateTimeOffset now)
    {
        if (_exercises.Count >= MaximumExercises)
            throw new ArgumentException($"A workout has at most {MaximumExercises} exercises.", "exercises");
        if (plannedExerciseId is { } planned && _exercises.Any(x => x.PlannedExerciseId == planned))
            throw new ArgumentException("The planned exercise is already part of the workout.", nameof(plannedExerciseId));
        var exercise = LoggedExercise.Create(Id, exerciseId, plannedExerciseId, NextOrder(), block, groupLabel);
        _exercises.Add(exercise);
        UpdatedAt = now;
        return exercise;
    }

    /// <summary>Removes an exercise the client added. Planned exercises are skipped instead, when permitted.</summary>
    public void RemoveExercise(Guid loggedExerciseId, DateTimeOffset now)
    {
        var exercise = Exercise(loggedExerciseId);
        if (exercise.IsPlanned) throw new ArgumentException("A planned exercise cannot be removed; skip it if permitted.", "exercise");
        _exercises.Remove(exercise);
        UpdatedAt = now;
    }

    public LoggedExercise Exercise(Guid loggedExerciseId) =>
        _exercises.SingleOrDefault(x => x.Id == loggedExerciseId)
            ?? throw new KeyNotFoundException("The exercise is not part of the workout.");

    public LoggedSet Set(Guid loggedSetId) =>
        _exercises.SelectMany(x => x.Sets).SingleOrDefault(x => x.Id == loggedSetId)
            ?? throw new KeyNotFoundException("The set is not part of the workout.");

    public LoggedExercise ExerciseOf(LoggedSet set) => _exercises.Single(x => x.Id == set.LoggedExerciseId);

    public void ChangeNotes(string? notes, DateTimeOffset now)
    {
        Notes = TrackingText.Optional(notes, MaximumNotesLength, nameof(notes));
        UpdatedAt = now;
    }

    /// <summary>Finishes the workout (§18 "Complete Workout"); at least one set must have been done.</summary>
    public void Complete(DateTimeOffset now)
    {
        if (IsCompleted) throw new InvalidOperationException("The workout is already completed.");
        if (!_exercises.SelectMany(x => x.Sets).Any(x => x.Status == LoggedSetStatus.Completed))
            throw new ArgumentException("Log at least one set before completing the workout.", "sets");
        Status = WorkoutSessionStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Records that a set of the workout changed.</summary>
    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private int NextOrder() => _exercises.Count == 0 ? 0 : _exercises.Max(x => x.Order) + 1;
}

/// <summary>An exercise as done in a session, with its sets. It may follow a planned exercise or be added by the client.</summary>
public sealed class LoggedExercise : Entity
{
    public const int MaximumSets = 30;

    private readonly List<LoggedSet> _sets = [];

    private LoggedExercise() { }

    public Guid WorkoutSessionId { get; private set; }
    public Guid ExerciseId { get; private set; }

    /// <summary>The planned exercise row it follows (a reference only); null when the client added it.</summary>
    public Guid? PlannedExerciseId { get; private set; }
    public int Order { get; private set; }
    public WorkoutBlock Block { get; private set; }

    /// <summary>The set-group label in the plan ("A1", "B2"), kept as it was.</summary>
    public string? GroupLabel { get; private set; }
    public bool IsSkipped { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<LoggedSet> Sets => _sets;
    public bool IsPlanned => PlannedExerciseId.HasValue;

    internal static LoggedExercise Create(
        Guid sessionId, Guid exerciseId, Guid? plannedExerciseId, int order, WorkoutBlock block, string? groupLabel) => new()
    {
        WorkoutSessionId = sessionId,
        ExerciseId = TrackingText.Id(exerciseId, nameof(exerciseId)),
        PlannedExerciseId = plannedExerciseId == Guid.Empty
            ? throw new ArgumentException("Planned exercise identifier cannot be empty.", nameof(plannedExerciseId))
            : plannedExerciseId,
        Order = TrackingText.Order(order, nameof(order)),
        Block = Enum.IsDefined(block) ? block : throw new ArgumentException("Unknown workout block.", nameof(block)),
        GroupLabel = TrackingText.Optional(groupLabel, 10, nameof(groupLabel))
    };

    /// <summary>Logs a set at the end; <paramref name="plannedSetId"/> names the prescription it follows.</summary>
    public LoggedSet LogSet(Guid? plannedSetId, LoggedSetValues values, DateTimeOffset now)
    {
        if (IsSkipped) throw new ArgumentException("The exercise is skipped; resume it before logging sets.", "exercise");
        if (_sets.Count >= MaximumSets) throw new ArgumentException($"An exercise has at most {MaximumSets} sets.", "sets");
        if (plannedSetId is { } planned && _sets.Any(x => x.PlannedSetId == planned))
            throw new ArgumentException("The planned set is already logged.", nameof(plannedSetId));
        var set = LoggedSet.Create(Id, plannedSetId, _sets.Count == 0 ? 0 : _sets.Max(x => x.Order) + 1, values, now);
        _sets.Add(set);
        return set;
    }

    public void RemoveSet(Guid loggedSetId)
    {
        var set = _sets.SingleOrDefault(x => x.Id == loggedSetId) ?? throw new KeyNotFoundException("The set is not part of the exercise.");
        _sets.Remove(set);
    }

    /// <summary>
    /// Skips the whole exercise (§18), or resumes it. A skipped exercise has no completed sets; the caller checks
    /// that the plan permits skipping it.
    /// </summary>
    public void Skip(bool skipped)
    {
        if (skipped && _sets.Any(x => x.Status == LoggedSetStatus.Completed))
            throw new ArgumentException("Delete the exercise's completed sets before skipping it.", "isSkipped");
        IsSkipped = skipped;
    }

    public void ChangeNotes(string? notes) => Notes = TrackingText.Optional(notes, 1000, nameof(notes));
}

/// <summary>A set as performed (§18, §19, D3): reps, weight in kg, RIR, RPE or a duration, or a skipped set.</summary>
public sealed record LoggedSetValues(
    SetType Type = SetType.Normal,
    LoggedSetStatus Status = LoggedSetStatus.Completed,
    int? Reps = null,
    decimal? WeightKg = null,
    int? Rir = null,
    decimal? Rpe = null,
    int? DurationSeconds = null,
    string? Notes = null);

/// <summary>One set as performed. Volume is weight × reps (§20); a set without weight (bodyweight) has none.</summary>
public sealed class LoggedSet : Entity
{
    public const int MaximumReps = PrescribedSet.MaximumReps;
    public const decimal MaximumWeightKg = PrescribedSet.MaximumLoadKg;
    public const int MaximumDurationSeconds = PrescribedSet.MaximumDurationSeconds;

    private LoggedSet() { }

    public Guid LoggedExerciseId { get; private set; }

    /// <summary>The prescribed set it follows (a reference only).</summary>
    public Guid? PlannedSetId { get; private set; }
    public int Order { get; private set; }
    public SetType Type { get; private set; }
    public LoggedSetStatus Status { get; private set; }
    public int? Reps { get; private set; }
    public decimal? WeightKg { get; private set; }
    public int? Rir { get; private set; }
    public decimal? Rpe { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsCompleted => Status == LoggedSetStatus.Completed;

    /// <summary>Weight × reps for a completed set with both; zero otherwise.</summary>
    public decimal VolumeKg => IsCompleted && WeightKg is { } weight && Reps is { } reps ? weight * reps : 0m;

    public LoggedSetValues Values => new(Type, Status, Reps, WeightKg, Rir, Rpe, DurationSeconds, Notes);

    internal static LoggedSet Create(Guid loggedExerciseId, Guid? plannedSetId, int order, LoggedSetValues values, DateTimeOffset now)
    {
        var set = new LoggedSet
        {
            LoggedExerciseId = loggedExerciseId,
            PlannedSetId = plannedSetId == Guid.Empty
                ? throw new ArgumentException("Planned set identifier cannot be empty.", nameof(plannedSetId))
                : plannedSetId,
            Order = TrackingText.Order(order, nameof(order)),
            LoggedAt = now
        };
        set.Apply(values, now);
        return set;
    }

    public void Change(LoggedSetValues values, DateTimeOffset now) => Apply(values, now);

    private void Apply(LoggedSetValues values, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!Enum.IsDefined(values.Type)) throw new ArgumentException("Unknown set type.", "type");
        if (!Enum.IsDefined(values.Status)) throw new ArgumentException("Unknown set status.", "status");
        if (values.Status == LoggedSetStatus.Skipped)
        {
            if (values.Reps.HasValue || values.WeightKg.HasValue || values.Rir.HasValue || values.Rpe.HasValue || values.DurationSeconds.HasValue)
                throw new ArgumentException("A skipped set has no performance values.", "status");
        }
        else if (!values.Reps.HasValue && !values.DurationSeconds.HasValue)
        {
            throw new ArgumentException("Enter the reps or the duration of the set.", "reps");
        }
        if (values.WeightKg is { } weight && (weight < 0 || weight > MaximumWeightKg || decimal.Round(weight, 2) != weight))
            throw new ArgumentException($"Weight is 0–{MaximumWeightKg} kg with at most two decimals.", "weightKg");
        if (values.Rpe is { } rpe && (rpe < 1 || rpe > 10 || rpe * 2 != decimal.Truncate(rpe * 2)))
            throw new ArgumentException("RPE is 1–10 in steps of 0.5.", "rpe");

        Type = values.Type;
        Status = values.Status;
        Reps = TrackingText.Range(values.Reps, 0, MaximumReps, "reps");
        WeightKg = values.WeightKg;
        Rir = TrackingText.Range(values.Rir, 0, 10, "rir");
        Rpe = values.Rpe;
        DurationSeconds = TrackingText.Range(values.DurationSeconds, 1, MaximumDurationSeconds, "durationSeconds");
        Notes = TrackingText.Optional(values.Notes, 500, "notes");
        UpdatedAt = now;
    }
}

internal static class TrackingText
{
    public static string Required(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ArgumentException($"At most {maximum} characters.", parameter);
        return normalized;
    }

    public static string? Optional(string? value, int maximum, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximum, parameter);

    public static Guid Id(Guid value, string parameter) =>
        value == Guid.Empty ? throw new ArgumentException("Identifier is required.", parameter) : value;

    public static int Order(int value, string parameter) =>
        value < 0 ? throw new ArgumentOutOfRangeException(parameter) : value;

    public static int? Range(int? value, int minimum, int maximum, string name) =>
        value is { } number && (number < minimum || number > maximum)
            ? throw new ArgumentException($"Must be between {minimum} and {maximum}.", name)
            : value;
}
