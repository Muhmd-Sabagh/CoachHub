using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Nutrition;

/// <summary>
/// The food catalog (§5): platform foods managed by administrators, plus each coach's custom foods.
/// Searches cover both; the query filter hides other coaches' custom foods.
/// </summary>
public sealed class FoodService(
    IFoodRepository repository,
    IReferenceRepository<FoodCategory> categories,
    IMediaRepository mediaRepository,
    CoachOwnership ownership,
    ICurrentUser currentUser)
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
        // An administrator creates platform foods unless they name a coach; a coach creates their own.
        Guid? owner = currentUser.IsAdministrator && input.CoachId is null
            ? null
            : await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
        await ValidateReferencesAsync(input, owner, cancellationToken);
        var food = DomainValidation.Run(() => FoodItem.Create(owner, Values(input), UnitValues(input)), "food");
        await repository.AddAsync(food, cancellationToken);
        return Map(food);
    }

    public async Task<FoodResponse> UpdateAsync(
        Guid id,
        FoodInput input,
        CancellationToken cancellationToken)
    {
        var food = await FindWritableAsync(id, cancellationToken);
        await ValidateReferencesAsync(input, food.CoachId, cancellationToken);
        DomainValidation.Run(() => food.Update(Values(input), UnitValues(input)), "food");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(food);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var food = await FindWritableAsync(id, cancellationToken);
        await repository.DeleteAsync(food, cancellationToken);
    }

    internal static FoodValues Values(FoodInput input) => new(
        input.NameEn,
        input.NameAr,
        input.FoodCategoryId,
        input.BaseUnit,
        input.ServingSize ?? DefaultServing(input.BaseUnit),
        input.CaloriesPer100,
        input.ProteinPer100,
        input.CarbohydratesPer100,
        input.FatPer100,
        input.FiberPer100,
        input.SugarPer100,
        input.SodiumMgPer100,
        input.PieceNameEn,
        input.PieceNameAr,
        input.MediaId,
        input.IsActive);

    /// <summary>One piece, or 100 g / 100 ml, when no serving size is given.</summary>
    internal static decimal DefaultServing(FoodBaseUnit unit) => unit == FoodBaseUnit.Piece ? 1m : 100m;

    private static IReadOnlyCollection<FoodUnitValues> UnitValues(FoodInput input) =>
        (input.Units ?? []).Select(unit => new FoodUnitValues(unit.Id, unit.NameEn, unit.NameAr, unit.BaseQuantity)).ToArray();

    internal static FoodResponse Map(FoodItem food) => new(
        food.Id,
        food.CoachId,
        !food.IsPlatform,
        food.NameEn,
        food.NameAr,
        food.FoodCategoryId,
        food.BaseUnit,
        food.BaseUnitLabel,
        food.PieceNameEn,
        food.PieceNameAr,
        food.ServingSize,
        food.CaloriesPer100,
        food.ProteinPer100,
        food.CarbohydratesPer100,
        food.FatPer100,
        food.FiberPer100,
        food.SugarPer100,
        food.SodiumMgPer100,
        food.Units.OrderBy(unit => unit.Order)
            .Select(unit => new FoodUnitResponse(unit.Id, unit.NameEn, unit.NameAr, unit.BaseQuantity)).ToArray(),
        food.MediaId,
        food.IsActive);

    private async Task ValidateReferencesAsync(FoodInput input, Guid? owner, CancellationToken cancellationToken)
    {
        if (input.FoodCategoryId == Guid.Empty || await categories.FindAsync(input.FoodCategoryId, cancellationToken) is null)
            throw Validation("foodCategoryId", "The selected food category does not exist.");

        if (input.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(input.MediaId.Value, cancellationToken);
            // Platform foods show platform images only; a custom food may also use its coach's library.
            var allowed = MediaService.IsPlatformImage(media) ||
                (owner is { } coachId && MediaService.IsCoachLibraryImage(media, coachId));
            if (!allowed)
                throw Validation("mediaId", owner is null
                    ? "The selected media must be an existing platform image."
                    : "The selected media must be a platform image or an image in the coach's library.");
        }
    }

    /// <summary>A visible food the caller may change: administrators any, a coach only their own.</summary>
    private async Task<FoodItem> FindWritableAsync(Guid id, CancellationToken cancellationToken)
    {
        var food = await FindRequiredAsync(id, cancellationToken);
        if (!currentUser.IsAdministrator && food.CoachId != currentUser.CoachId)
            throw new ForbiddenException("Platform foods are managed by administrators. Create a custom food instead.");
        return food;
    }

    private async Task<FoodItem> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Food item", id);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
