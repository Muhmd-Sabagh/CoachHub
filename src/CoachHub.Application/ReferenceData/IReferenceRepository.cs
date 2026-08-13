using CoachHub.Application.Common.Models;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public interface IReferenceRepository<TEntity> where TEntity : ActiveReferenceEntity
{
    Task<PagedResult<TEntity>> ListAsync(ReferenceListQuery query, CancellationToken cancellationToken);
    Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> KeyExistsAsync(string key, Guid? excludingId, CancellationToken cancellationToken);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken);
}