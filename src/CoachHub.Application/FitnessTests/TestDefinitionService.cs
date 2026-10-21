using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.FitnessTests;

namespace CoachHub.Application.FitnessTests;

/// <summary>
/// The test library (§34–§36): platform tests managed by administrators, plus each coach's custom tests. Searches
/// cover both; the query filter hides other coaches' custom tests.
/// </summary>
public sealed class TestDefinitionService(
    IFitnessTestRepository repository,
    CoachOwnership ownership,
    ICurrentUser currentUser)
{
    public async Task<PagedResult<TestDefinitionResponse>> ListAsync(TestDefinitionQuery query, CancellationToken cancellationToken)
    {
        var page = await repository.ListDefinitionsAsync(query.Normalize(), cancellationToken);
        return new PagedResult<TestDefinitionResponse>(page.Items.Select(Map).ToArray(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<TestDefinitionResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindRequiredAsync(id, cancellationToken));

    public async Task<TestDefinitionResponse> CreateAsync(TestDefinitionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        // An administrator creates platform tests unless they name a coach; a coach creates their own.
        Guid? owner = currentUser.IsAdministrator && input.CoachId is null
            ? null
            : await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
        var test = DomainValidation.Run(() => TestDefinition.Create(owner, Values(input)), "test");
        repository.Add(test);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(test);
    }

    public async Task<TestDefinitionResponse> UpdateAsync(Guid id, TestDefinitionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var test = await FindWritableAsync(id, cancellationToken);
        DomainValidation.Run(() => test.Update(Values(input)), "test");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(test);
    }

    /// <summary>Deletes a test nobody uses; a test with assignments or results is deactivated instead (409).</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var test = await FindWritableAsync(id, cancellationToken);
        if (await repository.IsDefinitionInUseAsync(id, cancellationToken))
            throw new ConflictException("The test is assigned to clients or has results. Deactivate it instead.");
        await repository.DeleteDefinitionAsync(test, cancellationToken);
    }

    internal static TestDefinitionResponse Map(TestDefinition test) => new(
        test.Id, test.Kind, test.NameEn, test.NameAr, test.Unit, test.HigherIsBetter, test.GoodThreshold, test.PoorThreshold,
        test.InstructionsEn, test.InstructionsAr, test.YouTubeUrl, test.IsActive, test.CoachId, !test.IsPlatform);

    private static TestDefinitionValues Values(TestDefinitionInput input) => new(
        input.Kind, input.NameEn, input.NameAr, input.Unit, input.HigherIsBetter, input.GoodThreshold, input.PoorThreshold,
        input.InstructionsEn, input.InstructionsAr, input.YouTubeUrl, input.IsActive);

    /// <summary>A visible test the caller may change: administrators any, a coach only their own.</summary>
    private async Task<TestDefinition> FindWritableAsync(Guid id, CancellationToken cancellationToken)
    {
        var test = await FindRequiredAsync(id, cancellationToken);
        if (!currentUser.IsAdministrator && test.CoachId != currentUser.CoachId)
            throw new ForbiddenException("Platform tests are managed by administrators. Create a custom test instead.");
        return test;
    }

    private async Task<TestDefinition> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindDefinitionAsync(id, cancellationToken) ?? throw new NotFoundException("Test", id);
}
