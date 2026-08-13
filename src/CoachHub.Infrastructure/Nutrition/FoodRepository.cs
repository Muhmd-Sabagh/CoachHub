using CoachHub.Application.Common.Models;
using CoachHub.Application.Nutrition;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Nutrition;

public sealed class FoodRepository(CoachHubDbContext dbContext) : IFoodRepository
{
    public async Task<PagedResult<FoodItem>> ListAsync(
        FoodQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<FoodItem> foods = dbContext.Set<FoodItem>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            foods = foods.Where(food =>
                food.NameEn.Contains(query.SearchTerm) ||
                (food.NameAr != null && food.NameAr.Contains(query.SearchTerm)) ||
                food.MeasurementUnit.Contains(query.SearchTerm));
        }
        if (query.CategoryId.HasValue)
        {
            foods = foods.Where(food => food.FoodCategoryId == query.CategoryId.Value);
        }
        if (query.IsActive.HasValue)
        {
            foods = foods.Where(food => food.IsActive == query.IsActive.Value);
        }

        foods = ApplySort(foods, query.SortBy, query.SortDescending);
        var total = await foods.LongCountAsync(cancellationToken);
        var page = await foods
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<FoodItem>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<FoodItem?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<FoodItem>().SingleOrDefaultAsync(food => food.Id == id, cancellationToken);

    public async Task AddAsync(FoodItem food, CancellationToken cancellationToken)
    {
        dbContext.Set<FoodItem>().Add(food);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(FoodItem food, CancellationToken cancellationToken)
    {
        dbContext.Set<FoodItem>().Remove(food);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LegacyFoodImportRecord?> FindLegacyImportAsync(
        int legacyId,
        CancellationToken cancellationToken) =>
        dbContext.Set<LegacyFoodImportRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.LegacyId == legacyId, cancellationToken);

    public async Task<FoodCategory> GetOrCreateUncategorizedAsync(
        CancellationToken cancellationToken)
    {
        const string categoryName = "Uncategorized";
        var category = await dbContext.Set<FoodCategory>()
            .SingleOrDefaultAsync(item => item.NameEn == categoryName, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = FoodCategory.Create(categoryName, null, true);
        dbContext.Set<FoodCategory>().Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task AddImportedAsync(
        FoodItem food,
        LegacyFoodImportRecord importRecord,
        CancellationToken cancellationToken)
    {
        dbContext.Set<FoodItem>().Add(food);
        dbContext.Set<LegacyFoodImportRecord>().Add(importRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<FoodItem> ApplySort(
        IQueryable<FoodItem> foods,
        string? sortBy,
        bool descending)
    {
        IOrderedQueryable<FoodItem> sorted = sortBy switch
        {
            "calories" => descending
                ? foods.OrderByDescending(food => food.CaloriesPer100)
                : foods.OrderBy(food => food.CaloriesPer100),
            "protein" => descending
                ? foods.OrderByDescending(food => food.ProteinPer100)
                : foods.OrderBy(food => food.ProteinPer100),
            "active" => descending
                ? foods.OrderByDescending(food => food.IsActive)
                : foods.OrderBy(food => food.IsActive),
            _ => descending
                ? foods.OrderByDescending(food => food.NameEn)
                : foods.OrderBy(food => food.NameEn)
        };

        return sorted.ThenBy(food => food.Id);
    }
}