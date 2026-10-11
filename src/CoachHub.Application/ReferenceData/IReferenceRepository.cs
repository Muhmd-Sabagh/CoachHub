using CoachHub.Application.Common.Models;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public interface IReferenceRepository<TEntity> where TEntity : ActiveReferenceEntity
{
    Task<PagedResult<TEntity>> ListAsync(ReferenceListQuery query, CancellationToken cancellationToken);
    Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken);
    /// <param name="coachId">For coach-owned reference data, uniqueness is checked within this coach only.</param>
    Task<bool> KeyExistsAsync(string key, Guid? excludingId, Guid? coachId, CancellationToken cancellationToken);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken);
}