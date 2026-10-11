using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public abstract class ReferenceDataService<TEntity, TInput, TResponse>(
    IReferenceRepository<TEntity> repository)
    where TEntity : ActiveReferenceEntity
{
    public async Task<PagedResult<TResponse>> ListAsync(
        ReferenceListQuery query,
        CancellationToken cancellationToken)
    {
        var page = await repository.ListAsync(query.Normalize(), cancellationToken);
        return new PagedResult<TResponse>(
            page.Items.Select(Map).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<TResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindRequiredAsync(id, cancellationToken));

    public async Task<TResponse> CreateAsync(TInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var ownerCoachId = await ResolveOwnerAsync(input, cancellationToken);
        await EnsureUniqueAsync(UniquenessKey(input), null, ownerCoachId, cancellationToken);
        var entity = Create(input, ownerCoachId);
        await repository.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<TResponse> UpdateAsync(Guid id, TInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var entity = await FindRequiredAsync(id, cancellationToken);
        await EnsureUniqueAsync(UniquenessKey(input), id, OwnerOf(entity), cancellationToken);
        Update(entity, input);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await FindRequiredAsync(id, cancellationToken);
        await repository.DeleteAsync(entity, cancellationToken);
    }

    protected abstract string ResourceName { get; }
    protected abstract void Validate(TInput input);
    protected abstract string UniquenessKey(TInput input);
    protected abstract TEntity Create(TInput input, Guid? ownerCoachId);

    /// <summary>Coach-owned reference data resolves its owning coach; platform data has none.</summary>
    protected virtual Task<Guid?> ResolveOwnerAsync(TInput input, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);

    protected virtual Guid? OwnerOf(TEntity entity) => null;
    protected abstract void Update(TEntity entity, TInput input);
    protected abstract TResponse Map(TEntity entity);

    protected static void ThrowValidation(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    protected static void Required(
        Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["A value is required."];
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors[field] = [$"The value cannot exceed {maximumLength} characters."];
        }
    }

    protected static void Optional(
        Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maximumLength)
        {
            errors[field] = [$"The value cannot exceed {maximumLength} characters."];
        }
    }

    private async Task<TEntity> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException(ResourceName, id);

    private async Task EnsureUniqueAsync(
        string key,
        Guid? excludingId,
        Guid? ownerCoachId,
        CancellationToken cancellationToken)
    {
        if (await repository.KeyExistsAsync(key.Trim(), excludingId, ownerCoachId, cancellationToken))
        {
            throw new ConflictException($"{ResourceName} with the same business key already exists.");
        }
    }
}