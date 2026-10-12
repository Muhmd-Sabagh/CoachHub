using System.Text.Json.Serialization;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.Nutrition;

/// <summary>
/// A food as entered (§5). Nutrients are per 100 base units; fiber and sugar in grams, sodium in
/// milligrams. <paramref name="CoachId"/> is for administrators creating a coach's custom food;
/// without it an administrator creates a platform food, and a coach always creates their own.
/// </summary>
public sealed record FoodInput(
    string NameEn,
    string? NameAr,
    Guid FoodCategoryId,
    FoodBaseUnit BaseUnit,
    decimal CaloriesPer100,
    decimal ProteinPer100,
    decimal CarbohydratesPer100,
    decimal FatPer100,
    Guid? MediaId,
    bool IsActive = true,
    decimal? FiberPer100 = null,
    decimal? SugarPer100 = null,
    decimal? SodiumMgPer100 = null,
    decimal? ServingSize = null,
    string? PieceNameEn = null,
    string? PieceNameAr = null,
    IReadOnlyList<FoodUnitInput>? Units = null,
    Guid? CoachId = null);

/// <summary>A named unit worth <paramref name="BaseQuantity"/> base units; send the id to keep an existing unit.</summary>
public sealed record FoodUnitInput(Guid? Id, string NameEn, string? NameAr, decimal BaseQuantity);

public sealed record FoodUnitResponse(Guid Id, string NameEn, string? NameAr, decimal BaseQuantity);

public sealed record FoodResponse(
    Guid Id,
    Guid? CoachId,
    bool IsCustom,
    string NameEn,
    string? NameAr,
    Guid FoodCategoryId,
    FoodBaseUnit BaseUnit,
    string BaseUnitLabel,
    string? PieceNameEn,
    string? PieceNameAr,
    decimal ServingSize,
    decimal CaloriesPer100,
    decimal ProteinPer100,
    decimal CarbohydratesPer100,
    decimal FatPer100,
    decimal? FiberPer100,
    decimal? SugarPer100,
    decimal? SodiumMgPer100,
    IReadOnlyList<FoodUnitResponse> Units,
    Guid? MediaId,
    bool IsActive);

/// <summary>Which foods a search covers: both catalogs (default), the platform's, or the coach's own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FoodScope>))]
public enum FoodScope { All, Platform, Custom }

public sealed record FoodQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public FoodScope Scope { get; init; } = FoodScope.All;
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
