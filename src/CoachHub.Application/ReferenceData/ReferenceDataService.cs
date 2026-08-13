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
        await EnsureUniqueAsync(UniquenessKey(input), null, cancellationToken);
        var entity = Create(input);
        await repository.AddAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<TResponse> UpdateAsync(Guid id, TInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var entity = await FindRequiredAsync(id, cancellationToken);
        await EnsureUniqueAsync(UniquenessKey(input), id, cancellationToken);
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
    protected abstract TEntity Create(TInput input);
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
        CancellationToken cancellationToken)
    {
        if (await repository.KeyExistsAsync(key.Trim(), excludingId, cancellationToken))
        {
            throw new ConflictException($"{ResourceName} with the same business key already exists.");
        }
    }
}