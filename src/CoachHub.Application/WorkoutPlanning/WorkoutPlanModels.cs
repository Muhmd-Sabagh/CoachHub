using CoachHub.Application.Common.Models;
using CoachHub.Domain.Common;

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
public sealed record WorkoutDayInput(
    Guid Id, string NameEn, string? NameAr, string? Subtitle, string? Notes, int Order,
    IReadOnlyCollection<WorkoutExerciseInput> Exercises);
public sealed record WorkoutExerciseInput(
    Guid Id, Guid ExerciseId, int Order, string? Sets, string? Repetitions,
    string? Rest, string? Tempo, string? RpeRir, string? Notes);
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
    IReadOnlyList<WorkoutExerciseResponse> Exercises);
public sealed record WorkoutExerciseResponse(
    Guid Id, Guid ExerciseId, string ExerciseNameEn, string? ExerciseNameAr,
    Guid ExerciseCategoryId, Guid? MediaId, string? YouTubeUrl, bool ExerciseIsActive,
    int Order, string? Sets, string? Repetitions, string? Rest, string? Tempo,
    string? RpeRir, string? Notes);
