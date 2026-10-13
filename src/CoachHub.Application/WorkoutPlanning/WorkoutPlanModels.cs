using CoachHub.Application.Common.Models;
using CoachHub.Domain.Common;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

/// <summary>
/// A workout plan's name and assignment plus the content of one version (D4): program dates, Draft/Active
/// status, notes and days. <paramref name="BaseVersionNumber"/> is the version the editor loaded.
/// </summary>
public sealed record WorkoutPlanInput(
    string NameEn, string? NameAr, Guid? ClientId,
    IReadOnlyCollection<WorkoutPlanNoteInput> Notes,
    IReadOnlyCollection<WorkoutDayInput> Days,
    Guid? CoachId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    PlanRevisionStatus Status = PlanRevisionStatus.Active,
    int? BaseVersionNumber = null);
public sealed record WorkoutPlanNoteInput(Guid Id, string Text, int Order, bool IsActive);
/// <summary>A training day; <paramref name="SetGroups"/> are its supersets and giant sets.</summary>
public sealed record WorkoutDayInput(
    Guid Id, string NameEn, string? NameAr, string? Subtitle, string? Notes, int Order,
    IReadOnlyCollection<WorkoutExerciseInput> Exercises,
    IReadOnlyCollection<WorkoutSetGroupInput>? SetGroups = null);
/// <summary>Exercises done back to back: a superset (2) or giant set (3+), with the rest after each round.</summary>
public sealed record WorkoutSetGroupInput(Guid Id, SetGroupType Type, int? RestSeconds = null, string? Notes = null);
/// <summary>
/// An exercise of the day (§16) in a block (warm-up, main, cool-down), optionally in one of the day's set groups,
/// with one typed prescription per set (D3).
/// </summary>
public sealed record WorkoutExerciseInput(
    Guid Id, Guid ExerciseId, int Order, string? Notes,
    IReadOnlyCollection<PrescribedSetInput> Sets,
    WorkoutBlock Block = WorkoutBlock.Main,
    Guid? SetGroupId = null);
/// <summary>One set: reps exact (<paramref name="RepsMin"/> only) or a range, load in kg, RIR, RPE, tempo, rest, duration.</summary>
public sealed record PrescribedSetInput(
    Guid Id, int Order, SetType Type = SetType.Normal,
    int? RepsMin = null, int? RepsMax = null, decimal? LoadKg = null, int? Rir = null, decimal? Rpe = null,
    Tempo? Tempo = null, int? RestSeconds = null, int? DurationSeconds = null, bool IsSkippable = false, string? Notes = null);
public sealed record CopyWorkoutPlanInput(string NameEn, string? NameAr, Guid? ClientId);
public sealed record AssignWorkoutPlanInput(Guid? ClientId);
public sealed record SetWorkoutPlanNoteActiveInput(bool IsActive);

/// <summary>A workout plan as one of its versions: the current one, or an archived one (read-only).</summary>
public sealed record WorkoutPlanResponse(
    Guid Id, Guid CoachId, string NameEn, string? NameAr, Guid? ClientId, DateTimeOffset CreatedAt,
    PlanRevisionInfo Revision, int CurrentVersionNumber,
    IReadOnlyList<WorkoutPlanNoteResponse> Notes, IReadOnlyList<WorkoutDayResponse> Days);
public sealed record WorkoutPlanNoteResponse(Guid Id, string Text, int Order, bool IsActive);
public sealed record WorkoutDayResponse(
    Guid Id, string NameEn, string? NameAr, string? Subtitle, string? Notes, int Order,
    IReadOnlyList<WorkoutExerciseResponse> Exercises, IReadOnlyList<WorkoutSetGroupResponse> SetGroups);
/// <summary>A set group; <paramref name="Label"/> is its letter ("A", "B"), and members are "A1", "A2", ….</summary>
public sealed record WorkoutSetGroupResponse(Guid Id, SetGroupType Type, string Label, int? RestSeconds, string? Notes);
public sealed record WorkoutExerciseResponse(
    Guid Id, Guid ExerciseId, string ExerciseNameEn, string? ExerciseNameAr,
    Guid ExerciseCategoryId, Guid? MediaId, string? YouTubeUrl, bool ExerciseIsActive,
    int Order, string? Notes, WorkoutBlock Block, Guid? SetGroupId, string? GroupLabel,
    Guid? PrimaryMuscleGroupId, IReadOnlyList<PrescribedSetResponse> Sets);
public sealed record PrescribedSetResponse(
    Guid Id, int Order, SetType Type, int? RepsMin, int? RepsMax, decimal? LoadKg, int? Rir, decimal? Rpe,
    Tempo? Tempo, int? RestSeconds, int? DurationSeconds, bool IsSkippable, string? Notes);
