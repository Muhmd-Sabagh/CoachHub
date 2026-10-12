using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.WorkoutPlanning;

public sealed class WorkoutPlanRepository(CoachHubDbContext dbContext) : IWorkoutPlanRepository
{
    public async Task<WorkoutPlanAggregate?> FindAsync(Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken)
    {
        var plan = await Query<WorkoutPlan>(tracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return null;
        var revisions = await Query<WorkoutPlanRevision>(tracking).Where(x => x.PlanId == id).ToListAsync(cancellationToken);
        var current = revisions.SingleOrDefault(x => !x.IsArchived);
        var revision = versionNumber is { } number ? revisions.SingleOrDefault(x => x.VersionNumber == number) : current;
        if (revision is null || current is null) return null;
        var revisionId = revision.Id;
        return new(plan, revision, current.VersionNumber,
            await Query<WorkoutPlanNote>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<WorkoutDay>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken),
            await Query<WorkoutExercise>(tracking).Where(x => x.RevisionId == revisionId).ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<WorkoutPlanRevision>?> ListRevisionsAsync(Guid planId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Set<WorkoutPlan>().AnyAsync(x => x.Id == planId, cancellationToken)) return null;
        return await dbContext.Set<WorkoutPlanRevision>().AsNoTracking().Where(x => x.PlanId == planId).ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyDictionary<Guid, string>> FindAuthorNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        PlanAuthors.FindNamesAsync(dbContext, userIds, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Exercise>> FindExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<Exercise>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        dbContext.Add(aggregate.Plan); dbContext.Add(aggregate.Revision); AddContent(aggregate);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceContentAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        RemoveContent([aggregate.Revision.Id]);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear(); AddContent(aggregate); await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddRevisionAsync(WorkoutPlanAggregate next, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        // Archive the previous version first: only one version per plan may be current.
        await PlanAuthors.SaveVersionAsync(dbContext, cancellationToken);
        dbContext.Add(next.Revision); AddContent(next);
        await PlanAuthors.SaveVersionAsync(dbContext, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid planId, CancellationToken cancellationToken)
    {
        var revisionIds = await dbContext.Set<WorkoutPlanRevision>().Where(x => x.PlanId == planId)
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        RemoveContent(revisionIds);
        dbContext.RemoveRange(dbContext.Set<WorkoutPlanRevision>().Where(x => x.PlanId == planId));
        dbContext.RemoveRange(dbContext.Set<WorkoutPlan>().Where(x => x.Id == planId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
    private IQueryable<T> Query<T>(bool tracking) where T : class => tracking ? dbContext.Set<T>() : dbContext.Set<T>().AsNoTracking();
    private void RemoveContent(Guid[] revisionIds)
    {
        dbContext.RemoveRange(dbContext.Set<WorkoutExercise>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<WorkoutDay>().Where(x => revisionIds.Contains(x.RevisionId)));
        dbContext.RemoveRange(dbContext.Set<WorkoutPlanNote>().Where(x => revisionIds.Contains(x.RevisionId)));
    }
    private void AddContent(WorkoutPlanAggregate aggregate)
    {
        dbContext.AddRange(aggregate.Notes); dbContext.AddRange(aggregate.Days); dbContext.AddRange(aggregate.Exercises);
    }
}
