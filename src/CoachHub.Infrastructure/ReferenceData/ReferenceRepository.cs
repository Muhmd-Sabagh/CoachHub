using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.ReferenceData;

public sealed class ReferenceRepository<TEntity>(CoachHubDbContext dbContext)
    : IReferenceRepository<TEntity> where TEntity : ActiveReferenceEntity
{
    public async Task<PagedResult<TEntity>> ListAsync(
        ReferenceListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<TEntity> items = dbContext.Set<TEntity>().AsNoTracking();

        if (query.IsActive.HasValue)
        {
            items = items.Where(item => item.IsActive == query.IsActive.Value);
        }

        items = ApplySearch(items, query.SearchTerm);
        items = ApplySort(items, query.SortBy, query.SortDescending);

        var total = await items.LongCountAsync(cancellationToken);
        var page = await items
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<TEntity>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<TEntity>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> KeyExistsAsync(
        string key,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = key.Trim().ToUpper();

        if (typeof(TEntity) == typeof(Package))
        {
            return dbContext.Set<Package>().AnyAsync(
                item => item.NameEn.ToUpper() == normalized && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);
        }
        if (typeof(TEntity) == typeof(FoodCategory))
        {
            return dbContext.Set<FoodCategory>().AnyAsync(
                item => item.NameEn.ToUpper() == normalized && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);
        }
        if (typeof(TEntity) == typeof(ExerciseCategory))
        {
            return dbContext.Set<ExerciseCategory>().AnyAsync(
                item => item.NameEn.ToUpper() == normalized && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);
        }
        if (typeof(TEntity) == typeof(Currency))
        {
            return dbContext.Set<Currency>().AnyAsync(
                item => item.Code.ToUpper() == normalized && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);
        }
        if (typeof(TEntity) == typeof(PaymentAccount))
        {
            return dbContext.Set<PaymentAccount>().AnyAsync(
                item => item.Name.ToUpper() == normalized && (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);
        }

        throw UnsupportedType();
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        dbContext.Set<TEntity>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (entity is FoodCategory category && await dbContext.Set<FoodItem>()
                .AnyAsync(food => food.FoodCategoryId == category.Id, cancellationToken))
        {
            throw new ConflictException(
                "Food category cannot be deleted while food items reference it. Deactivate it instead.");
        }

        dbContext.Set<TEntity>().Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<TEntity> ApplySearch(IQueryable<TEntity> source, string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return source;
        }

        if (typeof(TEntity) == typeof(Package))
        {
            var typed = (IQueryable<Package>)(object)source;
            return (IQueryable<TEntity>)(object)typed.Where(item =>
                item.NameEn.Contains(term) ||
                (item.NameAr != null && item.NameAr.Contains(term)) ||
                (item.Description != null && item.Description.Contains(term)));
        }
        if (typeof(TEntity) == typeof(FoodCategory))
        {
            var typed = (IQueryable<FoodCategory>)(object)source;
            return (IQueryable<TEntity>)(object)typed.Where(item =>
                item.NameEn.Contains(term) || (item.NameAr != null && item.NameAr.Contains(term)));
        }
        if (typeof(TEntity) == typeof(ExerciseCategory))
        {
            var typed = (IQueryable<ExerciseCategory>)(object)source;
            return (IQueryable<TEntity>)(object)typed.Where(item =>
                item.NameEn.Contains(term) || (item.NameAr != null && item.NameAr.Contains(term)));
        }
        if (typeof(TEntity) == typeof(Currency))
        {
            var typed = (IQueryable<Currency>)(object)source;
            return (IQueryable<TEntity>)(object)typed.Where(item =>
                item.Code.Contains(term) || item.Name.Contains(term) ||
                (item.Symbol != null && item.Symbol.Contains(term)));
        }
        if (typeof(TEntity) == typeof(PaymentAccount))
        {
            var typed = (IQueryable<PaymentAccount>)(object)source;
            return (IQueryable<TEntity>)(object)typed.Where(item =>
                item.Name.Contains(term) || (item.Details != null && item.Details.Contains(term)));
        }

        throw UnsupportedType();
    }

    private static IQueryable<TEntity> ApplySort(
        IQueryable<TEntity> source,
        string? sortBy,
        bool descending)
    {
        if (sortBy == "active")
        {
            return descending
                ? source.OrderByDescending(item => item.IsActive).ThenBy(item => item.Id)
                : source.OrderBy(item => item.IsActive).ThenBy(item => item.Id);
        }

        if (typeof(TEntity) == typeof(Currency))
        {
            var typed = (IQueryable<Currency>)(object)source;
            var sorted = sortBy == "name"
                ? (descending ? typed.OrderByDescending(item => item.Name) : typed.OrderBy(item => item.Name))
                : (descending ? typed.OrderByDescending(item => item.Code) : typed.OrderBy(item => item.Code));
            return (IQueryable<TEntity>)(object)sorted.ThenBy(item => item.Id);
        }
        if (typeof(TEntity) == typeof(PaymentAccount))
        {
            var typed = (IQueryable<PaymentAccount>)(object)source;
            var sorted = descending ? typed.OrderByDescending(item => item.Name) : typed.OrderBy(item => item.Name);
            return (IQueryable<TEntity>)(object)sorted.ThenBy(item => item.Id);
        }
        if (typeof(TEntity) == typeof(Package))
        {
            var typed = (IQueryable<Package>)(object)source;
            var sorted = descending ? typed.OrderByDescending(item => item.NameEn) : typed.OrderBy(item => item.NameEn);
            return (IQueryable<TEntity>)(object)sorted.ThenBy(item => item.Id);
        }
        if (typeof(TEntity) == typeof(FoodCategory))
        {
            var typed = (IQueryable<FoodCategory>)(object)source;
            var sorted = descending ? typed.OrderByDescending(item => item.NameEn) : typed.OrderBy(item => item.NameEn);
            return (IQueryable<TEntity>)(object)sorted.ThenBy(item => item.Id);
        }
        if (typeof(TEntity) == typeof(ExerciseCategory))
        {
            var typed = (IQueryable<ExerciseCategory>)(object)source;
            var sorted = descending ? typed.OrderByDescending(item => item.NameEn) : typed.OrderBy(item => item.NameEn);
            return (IQueryable<TEntity>)(object)sorted.ThenBy(item => item.Id);
        }

        throw UnsupportedType();
    }

    private static InvalidOperationException UnsupportedType() =>
        new($"Unsupported reference entity type: {typeof(TEntity).Name}.");
}