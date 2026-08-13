namespace CoachHub.Application.Nutrition;

public sealed record FoodInput(
    string NameEn,
    string? NameAr,
    Guid FoodCategoryId,
    string MeasurementUnit,
    decimal CaloriesPer100,
    decimal ProteinPer100,
    decimal CarbohydratesPer100,
    decimal FatPer100,
    Guid? MediaId,
    bool IsActive = true);

public sealed record FoodResponse(
    Guid Id,
    string NameEn,
    string? NameAr,
    Guid FoodCategoryId,
    string MeasurementUnit,
    decimal CaloriesPer100,
    decimal ProteinPer100,
    decimal CarbohydratesPer100,
    decimal FatPer100,
    Guid? MediaId,
    bool IsActive);

public sealed record FoodQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public FoodQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim(),
        SortBy = string.IsNullOrWhiteSpace(SortBy) ? null : SortBy.Trim().ToLowerInvariant()
    };
}

public sealed record LegacyFoodImportRow(
    int LegacyId,
    string Name,
    string Unit,
    decimal CaloriesPer100Units,
    decimal ProteinPer100Units,
    decimal CarbsPer100Units,
    decimal FatPer100Units,
    string? ImagePath,
    Guid? MediaId,
    string? NameAr = null,
    string? CategoryName = null);

public sealed record LegacyFoodImportRowResult(
    int LegacyId,
    string Status,
    Guid? FoodItemId,
    IReadOnlyList<string> Messages);

public sealed record LegacyFoodImportResult(
    int ImportedCount,
    int AlreadyImportedCount,
    int InvalidCount,
    IReadOnlyList<LegacyFoodImportRowResult> Rows);