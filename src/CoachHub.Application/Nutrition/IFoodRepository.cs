using CoachHub.Application.Common.Models;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Nutrition;

public interface IFoodRepository
{
    Task<PagedResult<FoodItem>> ListAsync(FoodQuery query, CancellationToken cancellationToken);
    Task<FoodItem?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(FoodItem food, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(FoodItem food, CancellationToken cancellationToken);
    Task<LegacyFoodImportRecord?> FindLegacyImportAsync(int legacyId, CancellationToken cancellationToken);
    Task<FoodCategory> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken);
    Task AddImportedAsync(
        FoodItem food,
        LegacyFoodImportRecord importRecord,
        CancellationToken cancellationToken);
}