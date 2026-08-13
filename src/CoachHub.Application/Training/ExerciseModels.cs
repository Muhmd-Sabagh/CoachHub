namespace CoachHub.Application.Training;

public sealed record ExerciseInput(
    string NameEn,
    string? NameAr,
    Guid ExerciseCategoryId,
    Guid? MediaId,
    string? YouTubeUrl,
    bool IsActive = true);

public sealed record ExerciseResponse(
    Guid Id,
    string NameEn,
    string? NameAr,
    Guid ExerciseCategoryId,
    Guid? MediaId,
    string? YouTubeUrl,
    bool IsActive);

public sealed record ExerciseQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
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