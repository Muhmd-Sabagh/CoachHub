using CoachHub.Application.Common.Models;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;

namespace CoachHub.Application.Training;

public interface IExerciseRepository
{
    Task<PagedResult<Exercise>> ListAsync(ExerciseQuery query, CancellationToken cancellationToken);
    Task<Exercise?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Exercise exercise, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Exercise exercise, CancellationToken cancellationToken);
    Task<LegacyExerciseImportRecord?> FindLegacyImportAsync(int legacyId, CancellationToken cancellationToken);
    Task<ExerciseCategory> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken);
    Task AddImportedAsync(
        Exercise exercise,
        LegacyExerciseImportRecord importRecord,
        CancellationToken cancellationToken);
}