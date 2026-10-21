using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.FitnessTests;
using CoachHub.Domain.FitnessTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.FitnessTests;

/// <summary>
/// The test library (§34–§36). Writes: administrators manage platform tests; coaches their own custom ones (checked
/// in <see cref="TestDefinitionService"/>).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/fitness-tests")]
public sealed class FitnessTestsController(TestDefinitionService tests) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TestDefinitionResponse>> List([FromQuery] TestDefinitionQuery query, CancellationToken token) =>
        tests.ListAsync(query, token);

    [HttpGet("{id:guid}")]
    public Task<TestDefinitionResponse> Get(Guid id, CancellationToken token) => tests.GetAsync(id, token);

    [HttpPost]
    public async Task<ActionResult<TestDefinitionResponse>> Create(TestDefinitionInput input, CancellationToken token)
    {
        var created = await tests.CreateAsync(input, token);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<TestDefinitionResponse> Update(Guid id, TestDefinitionInput input, CancellationToken token) =>
        tests.UpdateAsync(id, input, token);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await tests.DeleteAsync(id, token);
        return NoContent();
    }
}

/// <summary>
/// A client's tests as their coach sees them: assignments, results recorded by either side, corrections and the
/// coach's classification. Another coach's client is not found.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/fitness-tests")]
public sealed class ClientFitnessTestsController(FitnessTestService tests) : ControllerBase
{
    [HttpGet]
    public Task<ClientTestsResponse> List(Guid clientId, CancellationToken token) => tests.ClientTestsAsync(clientId, token);

    [HttpPut("assignments/{testId:guid}")]
    public async Task<IActionResult> Assign(Guid clientId, Guid testId, CancellationToken token)
    {
        await tests.AssignAsync(clientId, testId, token);
        return NoContent();
    }

    [HttpDelete("assignments/{testId:guid}")]
    public async Task<IActionResult> Unassign(Guid clientId, Guid testId, CancellationToken token)
    {
        await tests.UnassignAsync(clientId, testId, token);
        return NoContent();
    }

    [HttpPost("results")]
    public Task<TestResultResponse> Record(Guid clientId, TestResultInput input, CancellationToken token) =>
        tests.RecordAsync(clientId, input, TestRecorder.Coach, token);

    [HttpPut("results/{resultId:guid}")]
    public Task<TestResultResponse> Change(Guid clientId, Guid resultId, TestResultChangeInput input, CancellationToken token) =>
        tests.ChangeAsync(clientId, resultId, input, TestRecorder.Coach, token);

    [HttpDelete("results/{resultId:guid}")]
    public async Task<IActionResult> Delete(Guid clientId, Guid resultId, CancellationToken token)
    {
        await tests.DeleteAsync(clientId, resultId, TestRecorder.Coach, token);
        return NoContent();
    }

    /// <summary>Overrides the classification (Good / NeedsImprovement / Poor); null returns to the test's cut-offs.</summary>
    [HttpPut("results/{resultId:guid}/classification")]
    public Task<TestResultResponse> Classify(Guid clientId, Guid resultId, TestClassificationInput input, CancellationToken token) =>
        tests.ClassifyAsync(clientId, resultId, input, token);
}
