using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.FitnessTests;
using CoachHub.Domain.FitnessTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's tests (§34–§36): the tests the coach assigned, with instructions and video, and results
/// over time. The client records assigned tests and changes only the results they recorded. The client always comes
/// from the token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/fitness-tests")]
public sealed class MyFitnessTestsController(FitnessTestService tests) : ControllerBase
{
    [HttpGet]
    public Task<ClientTestsResponse> List(CancellationToken token) => tests.ClientTestsAsync(ClientId(), token);

    [HttpPost("results")]
    public Task<TestResultResponse> Record(TestResultInput input, CancellationToken token) =>
        tests.RecordAsync(ClientId(), input, TestRecorder.Client, token);

    [HttpPut("results/{resultId:guid}")]
    public Task<TestResultResponse> Change(Guid resultId, TestResultChangeInput input, CancellationToken token) =>
        tests.ChangeAsync(ClientId(), resultId, input, TestRecorder.Client, token);

    [HttpDelete("results/{resultId:guid}")]
    public async Task<IActionResult> Delete(Guid resultId, CancellationToken token)
    {
        await tests.DeleteAsync(ClientId(), resultId, TestRecorder.Client, token);
        return NoContent();
    }

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
