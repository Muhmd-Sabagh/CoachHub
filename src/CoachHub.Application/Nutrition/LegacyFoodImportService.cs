using CoachHub.Application.Media;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.Nutrition;

public sealed class LegacyFoodImportService(
    IFoodRepository repository,
    IMediaRepository mediaRepository)
{
    public async Task<LegacyFoodImportResult> ImportAsync(
        IReadOnlyCollection<LegacyFoodImportRow> rows,
        CancellationToken cancellationToken)
    {
        var results = new List<LegacyFoodImportRowResult>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = row.LegacyId > 0
                ? await repository.FindLegacyImportAsync(row.LegacyId, cancellationToken)
                : null;
            if (existing is not null)
            {
                results.Add(new(row.LegacyId, "AlreadyImported", existing.FoodItemId, []));
                continue;
            }

            var errors = await ValidateRowAsync(row, cancellationToken);
            if (errors.Count > 0)
            {
                results.Add(new(row.LegacyId, "Invalid", null, errors));
                continue;
            }

            var category = await repository.GetOrCreateCategoryAsync(
                row.CategoryName ?? "Uncategorized", cancellationToken);
            // Imported foods are platform foods (no coach).
            var food = FoodItem.Create(null, Values(row, category.Id));
            var record = LegacyFoodImportRecord.Create(row.LegacyId, food.Id, DateTimeOffset.UtcNow);
            await repository.AddImportedAsync(food, record, cancellationToken);

            var messages = string.IsNullOrWhiteSpace(row.ImagePath) || row.MediaId.HasValue
                ? Array.Empty<string>()
                : ["Legacy image path was not retained. Upload the image through Media and update the food MediaId."];
            results.Add(new(row.LegacyId, "Imported", food.Id, messages));
        }

        return new LegacyFoodImportResult(
            results.Count(result => result.Status == "Imported"),
            results.Count(result => result.Status == "AlreadyImported"),
            results.Count(result => result.Status == "Invalid"),
            results);
    }

    private async Task<IReadOnlyList<string>> ValidateRowAsync(
        LegacyFoodImportRow row,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (row.LegacyId <= 0)
        {
            errors.Add("LegacyId must be positive.");
        }

        try
        {
            FoodItem.Create(null, Values(row, Guid.NewGuid()));
        }
        catch (ArgumentException exception)
        {
            errors.Add(exception.Message);
        }

        if (row.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(row.MediaId.Value, cancellationToken);
            if (!MediaService.IsPlatformImage(media))
            {
                errors.Add("MediaId must reference an existing platform image uploaded through Media.");
            }
        }

        return errors;
    }

    /// <summary>Legacy values are per 100 of the stated unit; counted units become pieces (D3).</summary>
    private static FoodValues Values(LegacyFoodImportRow row, Guid categoryId)
    {
        var (unit, pieceEn, pieceAr) = LegacyFoodUnits.Map(row.Unit);
        return new FoodValues(row.Name, row.NameAr, categoryId, unit, FoodService.DefaultServing(unit),
            row.CaloriesPer100Units, row.ProteinPer100Units, row.CarbsPer100Units, row.FatPer100Units,
            PieceNameEn: pieceEn, PieceNameAr: pieceAr, MediaId: row.MediaId);
    }
}
