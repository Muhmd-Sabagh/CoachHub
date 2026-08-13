using CoachHub.Application.DietPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.DietPlanning;

public sealed class DietPlanRepository(CoachHubDbContext dbContext) : IDietPlanRepository
{
    public async Task<DietPlanAggregate?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<DietPlan> plans = dbContext.Set<DietPlan>();
        if (!tracking) plans = plans.AsNoTracking();
        var plan = await plans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return null;
        var versions = await Query<DietPlanVersion>(tracking).Where(x => x.DietPlanId == id).ToListAsync(cancellationToken);
        var versionIds = versions.Select(x => x.Id).ToArray();
        var meals = await Query<Meal>(tracking).Where(x => versionIds.Contains(x.DietPlanVersionId)).ToListAsync(cancellationToken);
        var mealIds = meals.Select(x => x.Id).ToArray();
        var foods = await Query<MealFoodItem>(tracking).Where(x => mealIds.Contains(x.MealId)).ToListAsync(cancellationToken);
        var groups = await Query<DietReplacementGroup>(tracking).Where(x => versionIds.Contains(x.DietPlanVersionId)).ToListAsync(cancellationToken);
        var groupIds = groups.Select(x => x.Id).ToArray();
        var options = await Query<DietReplacementOption>(tracking).Where(x => groupIds.Contains(x.DietReplacementGroupId)).ToListAsync(cancellationToken);
        var notes = await Query<DietPlanNote>(tracking).Where(x => x.DietPlanId == id).ToListAsync(cancellationToken);
        return new(plan, notes, versions, meals, foods, groups, options);
    }

    public async Task<IReadOnlyDictionary<Guid, FoodItem>> FindFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<FoodItem>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

    public Task<bool> ClientExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().AnyAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        dbContext.Add(aggregate.Plan);
        AddChildren(aggregate);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceChildrenAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var planId = aggregate.Plan.Id;
        var versionIds = await dbContext.Set<DietPlanVersion>().Where(x => x.DietPlanId == planId).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var mealIds = await dbContext.Set<Meal>().Where(x => versionIds.Contains(x.DietPlanVersionId)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        var groupIds = await dbContext.Set<DietReplacementGroup>().Where(x => versionIds.Contains(x.DietPlanVersionId)).Select(x => x.Id).ToArrayAsync(cancellationToken);
        dbContext.RemoveRange(dbContext.Set<DietReplacementOption>().Where(x => groupIds.Contains(x.DietReplacementGroupId)));
        dbContext.RemoveRange(dbContext.Set<DietReplacementGroup>().Where(x => versionIds.Contains(x.DietPlanVersionId)));
        dbContext.RemoveRange(dbContext.Set<MealFoodItem>().Where(x => mealIds.Contains(x.MealId)));
        dbContext.RemoveRange(dbContext.Set<Meal>().Where(x => versionIds.Contains(x.DietPlanVersionId)));
        dbContext.RemoveRange(dbContext.Set<DietPlanNote>().Where(x => x.DietPlanId == planId));
        dbContext.RemoveRange(dbContext.Set<DietPlanVersion>().Where(x => x.DietPlanId == planId));
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        AddChildren(aggregate);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<T> Query<T>(bool tracking) where T : class =>
        tracking ? dbContext.Set<T>() : dbContext.Set<T>().AsNoTracking();
    private void AddChildren(DietPlanAggregate aggregate)
    {
        dbContext.AddRange(aggregate.Notes); dbContext.AddRange(aggregate.Versions);
        dbContext.AddRange(aggregate.Meals); dbContext.AddRange(aggregate.FoodItems);
        dbContext.AddRange(aggregate.ReplacementGroups); dbContext.AddRange(aggregate.ReplacementOptions);
    }
}
