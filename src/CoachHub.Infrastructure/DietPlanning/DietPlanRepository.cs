using CoachHub.Application.DietPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.DietPlanning;

public sealed class DietPlanRepository(CoachHubDbContext dbContext) : IDietPlanRepository
{
    public async Task<DietPlanAggregate?> FindAsync(Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken)
    {
        var plan = await Query<DietPlan>(tracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return null;
        var revisions = await Query<DietPlanRevision>(tracking).Where(x => x.PlanId == id).ToListAsync(cancellationToken);
        var current = revisions.SingleOrDefault(x => !x.IsArchived);
        var revision = versionNumber is { } number ? revisions.SingleOrDefault(x => x.VersionNumber == number) : current;
        if (revision is null || current is null) return null;
        var revisionId = revision.Id;
        return new(plan, revision, current.VersionNumber,
            await Query<DietPlanNote>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<DietPlanVariant>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<Meal>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<MealFoodItem>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<DietReplacementGroup>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<DietReplacementOption>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<DietPlanRevision>?> ListRevisionsAsync(Guid planId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<DietPlan>().AnyAsync(x => x.Id == planId, cancellationToken)) return null;
        return await dbContext.Set<DietPlanRevision>().AsNoTracking().Where(x => x.PlanId == planId).ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyDictionary<Guid, string>> FindAuthorNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        PlanAuthors.FindNamesAsync(dbContext, userIds, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, FoodItem>> FindFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<FoodItem>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

    // Deliberately cross-tenant: only called for foods a visible plan already references (see the port).
    public async Task<IReadOnlyDictionary<Guid, FoodItem>> FindReferencedFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<FoodItem>().IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        dbContext.Add(aggregate.Plan);
        dbContext.Add(aggregate.Revision);
        AddContent(aggregate);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceContentAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        Guid[] revisionIds = [aggregate.Revision.Id];
        RemoveContent(revisionIds);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        AddContent(aggregate);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddRevisionAsync(DietPlanAggregate next, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        // Archive the previous version first: only one version per plan may be current.
        await PlanAuthors.SaveVersionAsync(dbContext, cancellationToken);
        dbContext.Add(next.Revision);
        AddContent(next);
        await PlanAuthors.SaveVersionAsync(dbContext, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid planId, CancellationToken cancellationToken)
    {
        var revisionIds = await dbContext.Set<DietPlanRevision>().Where(x => x.PlanId == planId)
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        RemoveContent(revisionIds);
        dbContext.RemoveRange(dbContext.Set<DietPlanRevision>().Where(x => x.PlanId == planId));
        dbContext.RemoveRange(dbContext.Set<DietPlan>().Where(x => x.Id == planId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<T> Query<T>(bool tracking) where T : class =>
        tracking ? dbContext.Set<T>() : dbContext.Set<T>().AsNoTracking();

    private void RemoveContent(Guid[] revisionIds)
    {
        dbContext.RemoveRange(dbContext.Set<DietReplacementOption>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<DietReplacementGroup>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<MealFoodItem>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<Meal>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<DietPlanVariant>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<DietPlanNote>().Where(x => revisionIds.Contains(x.RevisionId)));
    }

    private void AddContent(DietPlanAggregate aggregate)
    {
        dbContext.AddRange(aggregate.Notes); dbContext.AddRange(aggregate.Variants);
        dbContext.AddRange(aggregate.Meals); dbContext.AddRange(aggregate.FoodItems);
        dbContext.AddRange(aggregate.ReplacementGroups); dbContext.AddRange(aggregate.ReplacementOptions);
    }
}
