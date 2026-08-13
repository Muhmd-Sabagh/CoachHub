namespace CoachHub.Application.WorkoutPlanning;

public sealed record WorkoutPlanInput(
    string NameEn, string? NameAr, Guid? ClientId,
    IReadOnlyCollection<WorkoutPlanNoteInput> Notes,
    IReadOnlyCollection<WorkoutDayInput> Days);
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

public sealed record WorkoutPlanResponse(
    Guid Id, string NameEn, string? NameAr, Guid? ClientId, DateTimeOffset CreatedAt,
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
