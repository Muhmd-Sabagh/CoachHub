using CoachHub.Application.Common.Models;
using CoachHub.Domain.FitnessTests;

namespace CoachHub.Application.FitnessTests;

/// <summary>Tests, assignments and results (§34–§36). Reads go through the tenant filters.</summary>
public interface IFitnessTestRepository
{
    Task<PagedResult<TestDefinition>> ListDefinitionsAsync(TestDefinitionQuery query, CancellationToken cancellationToken);
    Task<TestDefinition?> FindDefinitionAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TestDefinition>> FindDefinitionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Whether any client, of any coach, has the test assigned or a result of it.</summary>
    Task<bool> IsDefinitionInUseAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TestAssignment>> ListAssignmentsAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TestResult>> ListResultsAsync(Guid clientId, CancellationToken cancellationToken);
    Task<TestResult?> FindResultAsync(Guid clientId, Guid id, CancellationToken cancellationToken);

    void Add(TestDefinition test);
    void Add(TestAssignment assignment);
    void Add(TestResult result);
    void Remove(TestAssignment assignment);
    void Remove(TestResult result);

    /// <summary>Deletes the test; 409 when results or assignments still reference it.</summary>
    Task DeleteDefinitionAsync(TestDefinition test, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
