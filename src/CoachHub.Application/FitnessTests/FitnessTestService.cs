using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.FitnessTests;

namespace CoachHub.Application.FitnessTests;

/// <summary>
/// A client's tests (§34–§36): the coach assigns tests; the client records results of assigned tests, and the coach
/// records results of any of their tests, corrects results and overrides classifications. A client changes only the
/// results they recorded. Tenant filters decide who sees which client.
/// </summary>
public sealed class FitnessTestService(IFitnessTestRepository repository, TimeProvider timeProvider)
{
    /// <summary>How far back a result can be dated, so earlier tests on paper can be entered.</summary>
    public const int MaximumBackfillDays = 3650;

    /// <summary>The client's assigned tests and tests with results, each with its progress and history.</summary>
    public async Task<ClientTestsResponse> ClientTestsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var assignments = (await repository.ListAssignmentsAsync(clientId, cancellationToken)).ToDictionary(x => x.TestDefinitionId);
        var results = (await repository.ListResultsAsync(clientId, cancellationToken)).ToLookup(x => x.TestDefinitionId);
        var ids = assignments.Keys.Concat(results.Select(x => x.Key)).Distinct().ToArray();
        var definitions = (await repository.FindDefinitionsAsync(ids, cancellationToken)).ToDictionary(x => x.Id);

        var tests = new List<ClientTestResponse>();
        foreach (var id in ids)
        {
            var definition = definitions.GetValueOrDefault(id);
            var history = results[id].OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt).ToArray();
            var latest = history.FirstOrDefault();
            if (definition is null && latest is null) continue;
            // The test as it is now when visible; otherwise as the latest result recorded it. The results' own
            // direction drives the progress, so it stays consistent with what they were measured against.
            var higherIsBetter = latest?.HigherIsBetter ?? definition!.HigherIsBetter;
            assignments.TryGetValue(id, out var assignment);
            tests.Add(new ClientTestResponse(
                id,
                definition?.Kind ?? latest!.Kind,
                definition?.NameEn ?? latest!.NameEn,
                definition is null ? latest!.NameAr : definition.NameAr,
                latest?.Unit ?? definition!.Unit,
                higherIsBetter,
                definition is null ? null : TestDefinitionService.Map(definition),
                assignment is not null,
                assignment?.AssignedAt,
                TestProgressCalculator.Compute(history.Select(x => new TestSample(x.Date, x.Value, x.CreatedAt)), higherIsBetter),
                history.Select(Map).ToArray()));
        }
        return new ClientTestsResponse(clientId,
            tests.OrderBy(x => x.Kind).ThenBy(x => x.NameEn, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Assigns one of the coach's tests (platform or custom) to the client; assigning twice changes nothing.</summary>
    public async Task AssignAsync(Guid clientId, Guid testId, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var test = await UsableTestAsync(testId, coachId, "testDefinitionId", cancellationToken);
        var assignments = await repository.ListAssignmentsAsync(clientId, cancellationToken);
        if (assignments.Any(x => x.TestDefinitionId == test.Id)) return;
        repository.Add(TestAssignment.Create(coachId, clientId, test.Id, timeProvider.GetUtcNow()));
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes the assignment; the client's results of the test stay.</summary>
    public async Task UnassignAsync(Guid clientId, Guid testId, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var assignment = (await repository.ListAssignmentsAsync(clientId, cancellationToken)).SingleOrDefault(x => x.TestDefinitionId == testId)
            ?? throw new NotFoundException("Test assignment", testId);
        repository.Remove(assignment);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Records a result. A client records only assigned tests; the coach any of their active tests.</summary>
    public async Task<TestResultResponse> RecordAsync(Guid clientId, TestResultInput input, TestRecorder recorder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        if (input.TestDefinitionId == Guid.Empty) throw Validation("testDefinitionId", "Choose a test.");
        var test = await UsableTestAsync(input.TestDefinitionId, coachId, "testDefinitionId", cancellationToken);
        if (recorder == TestRecorder.Client &&
            (await repository.ListAssignmentsAsync(clientId, cancellationToken)).All(x => x.TestDefinitionId != test.Id))
            throw new ForbiddenException("Your coach has not assigned this test.");
        EnsureDate(input.Date);
        var result = DomainValidation.Run(
            () => TestResult.Create(test, coachId, clientId, input.Date, input.Value, input.Notes, recorder, timeProvider.GetUtcNow()), "value");
        repository.Add(result);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(result);
    }

    public async Task<TestResultResponse> ChangeAsync(
        Guid clientId, Guid resultId, TestResultChangeInput input, TestRecorder recorder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = await WritableResultAsync(clientId, resultId, recorder, cancellationToken);
        EnsureDate(input.Date);
        DomainValidation.Run(() => result.Change(input.Date, input.Value, input.Notes, timeProvider.GetUtcNow()), "value");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(result);
    }

    public async Task DeleteAsync(Guid clientId, Guid resultId, TestRecorder recorder, CancellationToken cancellationToken)
    {
        var result = await WritableResultAsync(clientId, resultId, recorder, cancellationToken);
        repository.Remove(result);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The coach's classification of a result (§34); null returns to the cut-offs.</summary>
    public async Task<TestResultResponse> ClassifyAsync(
        Guid clientId, Guid resultId, TestClassificationInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = await WritableResultAsync(clientId, resultId, TestRecorder.Coach, cancellationToken);
        DomainValidation.Run(() => result.OverrideClassification(input.Classification, timeProvider.GetUtcNow()), "classification");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(result);
    }

    internal static TestResultResponse Map(TestResult result) => new(
        result.Id, result.TestDefinitionId, result.Date, result.Value, result.Unit, result.Classification,
        result.IsClassificationOverridden, result.RecordedBy, result.Notes, result.CreatedAt);

    /// <summary>An active test of the client's coach or the platform.</summary>
    private async Task<TestDefinition> UsableTestAsync(Guid testId, Guid coachId, string field, CancellationToken cancellationToken)
    {
        var test = await repository.FindDefinitionAsync(testId, cancellationToken) ?? throw new NotFoundException("Test", testId);
        // Administrators see every coach's tests; a client may only use their coach's or the platform's.
        if (test.CoachId is { } owner && owner != coachId) throw Validation(field, "The test belongs to another coach.");
        if (!test.IsActive) throw Validation(field, "The test is inactive.");
        return test;
    }

    private async Task<TestResult> WritableResultAsync(Guid clientId, Guid resultId, TestRecorder recorder, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var result = await repository.FindResultAsync(clientId, resultId, cancellationToken)
            ?? throw new NotFoundException("Test result", resultId);
        if (recorder == TestRecorder.Client && result.RecordedBy == TestRecorder.Coach)
            throw new ForbiddenException("Your coach recorded this result. Ask them to change it.");
        return result;
    }

    private void EnsureDate(DateOnly date)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        // One day ahead allows for clients east of UTC.
        if (date > today.AddDays(1)) throw Validation("date", "A result cannot be dated in the future.");
        if (date < today.AddDays(-MaximumBackfillDays)) throw Validation("date", "The date is too far back.");
    }

    private async Task<Guid> ClientCoachAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientCoachIdAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
