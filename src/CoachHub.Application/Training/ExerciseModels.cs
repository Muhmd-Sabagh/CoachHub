using System.Text.Json.Serialization;
using CoachHub.Domain.Training;

namespace CoachHub.Application.Training;

/// <summary>
/// An exercise as entered (§17). The primary muscle group is required; secondary groups and equipment are
/// reference data. <paramref name="CoachId"/> is for administrators creating a coach's custom exercise;
/// without it an administrator creates a platform exercise, and a coach always creates their own.
/// </summary>
public sealed record ExerciseInput(
    string NameEn,
    string? NameAr,
    Guid ExerciseCategoryId,
    Guid? MediaId,
    string? YouTubeUrl,
    bool IsActive = true,
    ExerciseDifficulty? Difficulty = null,
    Guid? PrimaryMuscleGroupId = null,
    IReadOnlyList<Guid>? SecondaryMuscleGroupIds = null,
    IReadOnlyList<Guid>? EquipmentIds = null,
    string? InstructionsEn = null,
    string? InstructionsAr = null,
    string? CoachingCuesEn = null,
    string? CoachingCuesAr = null,
    string? CommonMistakesEn = null,
    string? CommonMistakesAr = null,
    Guid? CoachId = null);

public sealed record ExerciseResponse(
    Guid Id,
    string NameEn,
    string? NameAr,
    Guid ExerciseCategoryId,
    Guid? MediaId,
    string? YouTubeUrl,
    bool IsActive,
    Guid? CoachId = null,
    bool IsCustom = false,
    ExerciseDifficulty? Difficulty = null,
    Guid? PrimaryMuscleGroupId = null,
    IReadOnlyList<Guid>? SecondaryMuscleGroupIds = null,
    IReadOnlyList<Guid>? EquipmentIds = null,
    string? InstructionsEn = null,
    string? InstructionsAr = null,
    string? CoachingCuesEn = null,
    string? CoachingCuesAr = null,
    string? CommonMistakesEn = null,
    string? CommonMistakesAr = null);

/// <summary>Which exercises a search covers: both catalogs (default), the platform's, or the coach's own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExerciseScope>))]
public enum ExerciseScope { All, Platform, Custom }

public sealed record ExerciseQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public ExerciseScope Scope { get; init; } = ExerciseScope.All;
    /// <summary>Exercises training the group, as primary or secondary muscle.</summary>
    public Guid? MuscleGroupId { get; init; }
    public Guid? EquipmentId { get; init; }
    public ExerciseDifficulty? Difficulty { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public ExerciseQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim(),
        SortBy = string.IsNullOrWhiteSpace(SortBy) ? null : SortBy.Trim().ToLowerInvariant()
    };
}

public sealed record LegacyExerciseImportRow(
    int LegacyId,
    string Name,
    string? YouTubeLink,
    string? ImagePath,
    Guid? MediaId,
    string? NameAr = null,
    string? CategoryName = null);

public sealed record LegacyExerciseImportRowResult(
    int LegacyId,
    string Status,
    Guid? ExerciseId,
    IReadOnlyList<string> Messages);

public sealed record LegacyExerciseImportResult(
    int ImportedCount,
    int AlreadyImportedCount,
    int InvalidCount,
    IReadOnlyList<LegacyExerciseImportRowResult> Rows);
