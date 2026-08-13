using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.WorkoutPlanning;

public sealed class WorkoutPlanRepository(CoachHubDbContext dbContext) : IWorkoutPlanRepository
{
    public async Task<WorkoutPlanAggregate?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var plan = await Query<WorkoutPlan>(tracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return null;
        var days = await Query<WorkoutDay>(tracking).Where(x => x.WorkoutPlanId == id).ToListAsync(cancellationToken);
        var dayIds = days.Select(x => x.Id).ToArray();
        var exercises = await Query<WorkoutExercise>(tracking).Where(x => dayIds.Contains(x.WorkoutDayId)).ToListAsync(cancellationToken);
        var notes = await Query<WorkoutPlanNote>(tracking).Where(x => x.WorkoutPlanId == id).ToListAsync(cancellationToken);
        return new(plan, notes, days, exercises);
    }

    public async Task<IReadOnlyDictionary<Guid, Exercise>> FindExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<Exercise>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    public Task<bool> ClientExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().AnyAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        dbContext.Add(aggregate.Plan); AddChildren(aggregate); await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceChildrenAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var planId = aggregate.Plan.Id;
        var dayIds = await dbContext.Set<WorkoutDay>().Where(x => x.WorkoutPlanId == planId).Select(x => x.Id).ToArrayAsync(cancellationToken);
        dbContext.RemoveRange(dbContext.Set<WorkoutExercise>().Where(x => dayIds.Contains(x.WorkoutDayId)));
        dbContext.RemoveRange(dbContext.Set<WorkoutDay>().Where(x => x.WorkoutPlanId == planId));
        dbContext.RemoveRange(dbContext.Set<WorkoutPlanNote>().Where(x => x.WorkoutPlanId == planId));
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear(); AddChildren(aggregate); await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        dbContext.Remove(aggregate.Plan); await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
    private IQueryable<T> Query<T>(bool tracking) where T : class => tracking ? dbContext.Set<T>() : dbContext.Set<T>().AsNoTracking();
    private void AddChildren(WorkoutPlanAggregate aggregate)
    {
        dbContext.AddRange(aggregate.Notes); dbContext.AddRange(aggregate.Days); dbContext.AddRange(aggregate.Exercises);
    }
}
