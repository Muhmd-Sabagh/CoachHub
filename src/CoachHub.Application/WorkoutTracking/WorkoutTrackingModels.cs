using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Settings;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>
/// Starts a workout on <paramref name="Date"/> (the client's local day): a day of the plan in force, or a free
/// workout named <paramref name="NameEn"/> when <paramref name="WorkoutDayId"/> is left out.
/// </summary>
public sealed record StartWorkoutInput(DateOnly Date, Guid? WorkoutDayId = null, string? NameEn = null);
public sealed record WorkoutNotesInput(string? Notes);
public sealed record AddLoggedExerciseInput(Guid ExerciseId);
public sealed record SkipLoggedExerciseInput(bool IsSkipped);
public sealed record LoggedExerciseNotesInput(string? Notes);

/// <summary>
/// A set as performed (§18, D3). <paramref name="PlannedSetId"/> names the prescribed set it follows; it is set
/// when logging and ignored on updates. A skipped set (<see cref="LoggedSetStatus.Skipped"/>) needs a planned set
/// the coach marked skippable, and has no values.
/// </summary>
public sealed record LoggedSetInput(
    Guid? PlannedSetId = null,
    SetType Type = SetType.Normal,
    LoggedSetStatus Status = LoggedSetStatus.Completed,
    int? Reps = null,
    decimal? WeightKg = null,
    int? Rir = null,
    decimal? Rpe = null,
    int? DurationSeconds = null,
    string? Notes = null);

public sealed record WorkoutSessionQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public WorkoutSessionStatus? Status { get; init; }

    public WorkoutSessionQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100)
    };
}

/// <summary>A training day for the client: the workout in progress, the rotation of the plan in force, and the week.</summary>
public sealed record TrainingDayResponse(
    DateOnly Date,
    WorkoutSessionSummary? InProgress,
    TrainingPlanToday? Plan,
    TrainingWeekResponse Week,
    IReadOnlyList<WorkoutSessionSummary> Sessions);

/// <summary>
/// The plan in force and its rotation. <paramref name="SuggestedDayId"/> is the next day not yet done this week
/// (null when the week's rotation is complete); the client may start any day.
/// </summary>
public sealed record TrainingPlanToday(
    Guid PlanId, string NameEn, string? NameAr, int VersionNumber, Guid? SuggestedDayId,
    IReadOnlyList<TrainingPlanDay> Days);
public sealed record TrainingPlanDay(
    Guid Id, string NameEn, string? NameAr, string? Subtitle, int Order, int ExerciseCount, int SetCount, bool DoneThisWeek);

/// <summary>The ISO week of the day: the rotation's days (one workout each) and the workouts completed.</summary>
public sealed record TrainingWeekResponse(DateOnly WeekStart, int PlannedSessions, int CompletedSessions);

public sealed record WorkoutSessionSummary(
    Guid Id, DateOnly Date, WorkoutSessionStatus Status, string NameEn, string? NameAr,
    Guid? PlanId, int? VersionNumber, Guid? WorkoutDayId,
    int ExerciseCount, int CompletedSets, decimal VolumeKg, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);

/// <summary>
/// A workout with its exercises. <paramref name="Records"/> are the PRs it set; <paramref name="PlanAvailable"/> is
/// false when the plan it followed has since been deleted (the log stands on its own).
/// </summary>
public sealed record WorkoutSessionResponse(
    Guid Id, DateOnly Date, WorkoutSessionStatus Status, string NameEn, string? NameAr,
    Guid? PlanId, int? VersionNumber, Guid? WorkoutDayId, bool PlanAvailable, string? Notes,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    int CompletedSets, decimal VolumeKg,
    IReadOnlyList<LoggedExerciseResponse> Exercises,
    IReadOnlyList<PersonalRecordResponse> Records);

/// <summary>
/// An exercise of a workout: what the plan prescribes (<paramref name="Planned"/>, empty for an added exercise)
/// and what was done. <paramref name="CanSkip"/> is true when every prescribed set is skippable (§18).
/// </summary>
public sealed record LoggedExerciseResponse(
    Guid Id, Guid ExerciseId, string ExerciseNameEn, string? ExerciseNameAr, Guid? MediaId, string? YouTubeUrl,
    Guid? PlannedExerciseId, int Order, WorkoutBlock Block, string? GroupLabel, int? GroupRestSeconds,
    bool IsSkipped, bool CanSkip, string? Notes, string? PlannedNotes,
    IReadOnlyList<PrescribedSetResponse> Planned,
    IReadOnlyList<LoggedSetResponse> Sets,
    decimal VolumeKg);

public sealed record LoggedSetResponse(
    Guid Id, Guid? PlannedSetId, int Order, SetType Type, LoggedSetStatus Status,
    int? Reps, decimal? WeightKg, int? Rir, decimal? Rpe, int? DurationSeconds, string? Notes,
    decimal VolumeKg, decimal? EstimatedOneRepMaxKg, DateTimeOffset LoggedAt);

/// <summary>A PR (§21): "Bench Press — 90 kg × 8". <paramref name="Value"/> is kg, or reps for a reps record.</summary>
public sealed record PersonalRecordResponse(
    Guid Id, Guid ExerciseId, string ExerciseNameEn, string? ExerciseNameAr, PersonalRecordType Type,
    decimal Value, decimal? PreviousValue, decimal? WeightKg, int? Reps, Guid WorkoutSessionId, DateOnly AchievedOn);

/// <summary>The current best of each exercise and type, and the latest records.</summary>
public sealed record PersonalRecordsResponse(
    IReadOnlyList<PersonalRecordResponse> Current, IReadOnlyList<PersonalRecordResponse> Recent);

/// <summary>An exercise's performance history (§19), newest first, with its PR history (§21).</summary>
public sealed record ExerciseHistoryResponse(
    Guid ExerciseId, string NameEn, string? NameAr, Guid? PrimaryMuscleGroupId,
    IReadOnlyList<ExerciseHistoryEntry> Entries,
    IReadOnlyList<PersonalRecordResponse> Records);

/// <summary>One workout's figures for an exercise: work sets, reps and volume, the top set and the best e1RM.</summary>
public sealed record ExerciseHistoryEntry(
    Guid WorkoutSessionId, DateOnly Date, int Sets, int Reps, decimal VolumeKg,
    decimal? TopWeightKg, int? TopWeightReps, decimal? BestEstimatedOneRepMaxKg,
    IReadOnlyList<LoggedSetResponse> LoggedSets);

/// <summary>Training analytics (§20, §23) for a range: totals, each week (Monday first) and month, and each exercise.</summary>
public sealed record TrainingAnalyticsResponse(
    DateOnly From, DateOnly To, OneRepMaxFormula OneRepMaxFormula, decimal SecondaryMuscleWeight,
    TrainingPeriodSummary Summary,
    IReadOnlyList<TrainingPeriodSummary> Weeks,
    IReadOnlyList<TrainingPeriodSummary> Months,
    IReadOnlyList<ExerciseProgressResponse> Exercises);

/// <summary>
/// A period's training: completed workouts, distinct training days (frequency), scheduled workouts and adherence,
/// work sets, reps and volume, and sets and volume per muscle group (§23).
/// </summary>
public sealed record TrainingPeriodSummary(
    DateOnly Start, DateOnly End, int CompletedSessions, int TrainingDays, int ScheduledSessions, decimal? AdherencePercent,
    int Sets, int Reps, decimal VolumeKg, IReadOnlyList<MuscleVolumeResponse> Muscles);

/// <summary>
/// A muscle group's share of the work. Secondary muscles count with the coach's weight, so sets may be fractional.
/// <paramref name="MuscleGroupId"/> is null for exercises without a primary muscle (legacy imports).
/// </summary>
public sealed record MuscleVolumeResponse(
    Guid? MuscleGroupId, string NameEn, string? NameAr, decimal Sets, decimal VolumeKg, decimal SharePercent);

/// <summary>Weight and rep progression of an exercise (§20): one point per workout.</summary>
public sealed record ExerciseProgressResponse(
    Guid ExerciseId, string NameEn, string? NameAr, IReadOnlyList<ExerciseProgressPoint> Points);
public sealed record ExerciseProgressPoint(
    DateOnly Date, Guid WorkoutSessionId, decimal? TopWeightKg, int? TopWeightReps, int? MaxReps,
    decimal? BestEstimatedOneRepMaxKg, decimal VolumeKg, int Sets, int Reps);
