using CoachHub.Application.Common.Models;
using CoachHub.Application.Training;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Training;

public sealed class ExerciseRepository(CoachHubDbContext dbContext) : IExerciseRepository
{
    public async Task<PagedResult<Exercise>> ListAsync(
        ExerciseQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Exercise> exercises = dbContext.Set<Exercise>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            exercises = exercises.Where(exercise =>
                exercise.NameEn.Contains(query.SearchTerm) ||
                (exercise.NameAr != null && exercise.NameAr.Contains(query.SearchTerm)) ||
                (exercise.YouTubeUrl != null && exercise.YouTubeUrl.Contains(query.SearchTerm)));
        }
        if (query.CategoryId.HasValue)
        {
            exercises = exercises.Where(exercise =>
                exercise.ExerciseCategoryId == query.CategoryId.Value);
        }
        if (query.IsActive.HasValue)
        {
            exercises = exercises.Where(exercise => exercise.IsActive == query.IsActive.Value);
        }

        IOrderedQueryable<Exercise> sorted = query.SortBy == "active"
            ? (query.SortDescending
                ? exercises.OrderByDescending(exercise => exercise.IsActive)
                : exercises.OrderBy(exercise => exercise.IsActive))
            : (query.SortDescending
                ? exercises.OrderByDescending(exercise => exercise.NameEn)
                : exercises.OrderBy(exercise => exercise.NameEn));
        var ordered = sorted.ThenBy(exercise => exercise.Id);
        var total = await ordered.LongCountAsync(cancellationToken);
        var page = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<Exercise>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<Exercise?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Exercise>().SingleOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);

    public async Task AddAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        dbContext.Set<Exercise>().Add(exercise);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        dbContext.Set<Exercise>().Remove(exercise);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LegacyExerciseImportRecord?> FindLegacyImportAsync(
        int legacyId,
        CancellationToken cancellationToken) =>
        dbContext.Set<LegacyExerciseImportRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.LegacyId == legacyId, cancellationToken);

    public async Task<ExerciseCategory> GetOrCreateUncategorizedAsync(
        CancellationToken cancellationToken)
    {
        const string categoryName = "Uncategorized";
        var category = await dbContext.Set<ExerciseCategory>()
            .SingleOrDefaultAsync(item => item.NameEn == categoryName, cancellationToken);
        if (category is not null) return category;
        category = ExerciseCategory.Create(categoryName, null, true);
        dbContext.Set<ExerciseCategory>().Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task AddImportedAsync(
        Exercise exercise,
        LegacyExerciseImportRecord importRecord,
        CancellationToken cancellationToken)
    {
        dbContext.Set<Exercise>().Add(exercise);
        dbContext.Set<LegacyExerciseImportRecord>().Add(importRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}