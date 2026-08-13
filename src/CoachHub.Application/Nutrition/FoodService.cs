using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Nutrition;

public sealed class FoodService(
    IFoodRepository repository,
    IReferenceRepository<FoodCategory> categories,
    IMediaRepository mediaRepository)
{
    public async Task<PagedResult<FoodResponse>> ListAsync(
        FoodQuery query,
        CancellationToken cancellationToken)
    {
        var page = await repository.ListAsync(query.Normalize(), cancellationToken);
        return new PagedResult<FoodResponse>(
            page.Items.Select(Map).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<FoodResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindRequiredAsync(id, cancellationToken));

    public async Task<FoodResponse> CreateAsync(FoodInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(input, cancellationToken);
        var food = Create(input);
        await repository.AddAsync(food, cancellationToken);
        return Map(food);
    }

    public async Task<FoodResponse> UpdateAsync(
        Guid id,
        FoodInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(input, cancellationToken);
        var food = await FindRequiredAsync(id, cancellationToken);
        food.Update(
            input.NameEn,
            input.NameAr,
            input.FoodCategoryId,
            input.MeasurementUnit,
            input.CaloriesPer100,
            input.ProteinPer100,
            input.CarbohydratesPer100,
            input.FatPer100,
            input.MediaId,
            input.IsActive);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(food);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var food = await FindRequiredAsync(id, cancellationToken);
        await repository.DeleteAsync(food, cancellationToken);
    }

    internal static void Validate(FoodInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 255);
        Optional(errors, "nameAr", input.NameAr, 255);
        Required(errors, "measurementUnit", input.MeasurementUnit, 50);
        if (input.FoodCategoryId == Guid.Empty)
        {
            errors["foodCategoryId"] = ["A food category is required."];
        }
        Macro(errors, "caloriesPer100", input.CaloriesPer100, FoodItem.MaximumCaloriesPer100);
        Macro(errors, "proteinPer100", input.ProteinPer100, FoodItem.MaximumProteinPer100);
        Macro(
            errors,
            "carbohydratesPer100",
            input.CarbohydratesPer100,
            FoodItem.MaximumCarbohydratesPer100);
        Macro(errors, "fatPer100", input.FatPer100, FoodItem.MaximumFatPer100);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    internal static FoodItem Create(FoodInput input) => FoodItem.Create(
        input.NameEn,
        input.NameAr,
        input.FoodCategoryId,
        input.MeasurementUnit,
        input.CaloriesPer100,
        input.ProteinPer100,
        input.CarbohydratesPer100,
        input.FatPer100,
        input.MediaId,
        input.IsActive);

    internal static FoodResponse Map(FoodItem food) => new(
        food.Id,
        food.NameEn,
        food.NameAr,
        food.FoodCategoryId,
        food.MeasurementUnit,
        food.CaloriesPer100,
        food.ProteinPer100,
        food.CarbohydratesPer100,
        food.FatPer100,
        food.MediaId,
        food.IsActive);

    private async Task ValidateReferencesAsync(FoodInput input, CancellationToken cancellationToken)
    {
        if (await categories.FindAsync(input.FoodCategoryId, cancellationToken) is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["foodCategoryId"] = ["The selected food category does not exist."]
            });
        }

        if (input.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(input.MediaId.Value, cancellationToken);
            if (media is null || !media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["mediaId"] = ["The selected media must be an existing image."]
                });
            }
        }
    }

    private async Task<FoodItem> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Food item", id);

    private static void Required(
        Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["A value is required."];
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors[field] = [$"The value cannot exceed {maximumLength} characters."];
        }
    }

    private static void Optional(
        Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maximumLength)
        {
            errors[field] = [$"The value cannot exceed {maximumLength} characters."];
        }
    }

    private static void Macro(
        Dictionary<string, string[]> errors,
        string field,
        decimal value,
        decimal maximum)
    {
        if (value < 0 || value > maximum)
        {
            errors[field] = [$"The value must be between 0 and {maximum}."];
        }
    }
}