using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.CheckIns;

/// <summary>
/// Check-in management for coaches (§29): submitted check-ins and their history, pending and overdue check-ins, and the
/// review — reply, private notes, approve, request changes. Tenant filters limit a coach to their own clients.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/check-ins")]
public sealed class CheckInsController(CheckInService checkIns) : ControllerBase
{
    /// <summary>Submitted check-ins, newest first (filter by client and review status).</summary>
    [HttpGet]
    public Task<PagedResult<CheckInSummary>> List([FromQuery] CheckInQuery query, CancellationToken token) => checkIns.ListAsync(query, token);

    /// <summary>Scheduled clients by next due date: pending or overdue.</summary>
    [HttpGet("due")]
    public Task<PagedResult<CheckInDueResponse>> Due([FromQuery] CheckInDueQuery query, CancellationToken token) => checkIns.DueAsync(query, token);

    [HttpGet("{id:guid}")]
    public Task<CheckInDetail> Get(Guid id, CancellationToken token) => checkIns.GetAsync(id, token);

    /// <summary>A reply or feedback the client sees.</summary>
    [HttpPost("{id:guid}/replies")]
    public Task<CheckInDetail> Reply(Guid id, CheckInReplyInput input, CancellationToken token) => checkIns.ReplyAsync(id, input, token);

    /// <summary>The coach's private notes; never shown to the client.</summary>
    [HttpPut("{id:guid}/notes")]
    public Task<CheckInDetail> Notes(Guid id, CheckInNotesInput input, CancellationToken token) => checkIns.ChangeNotesAsync(id, input, token);

    [HttpPost("{id:guid}/approve")]
    public Task<CheckInDetail> Approve(Guid id, CheckInReviewInput input, CancellationToken token) => checkIns.ApproveAsync(id, input, token);

    /// <summary>Asks the client to change and resubmit; the message says what to change.</summary>
    [HttpPost("{id:guid}/request-changes")]
    public Task<CheckInDetail> RequestChanges(Guid id, CheckInReviewInput input, CancellationToken token) => checkIns.RequestChangesAsync(id, input, token);
}

/// <summary>A client's check-in schedule (§28), set by their coach.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/check-in-schedule")]
public sealed class ClientCheckInScheduleController(CheckInService checkIns) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CheckInScheduleResponse>> Get(Guid clientId, CancellationToken token) =>
        await checkIns.GetScheduleAsync(clientId, token) is { } schedule ? schedule : NoContent();

    [HttpPut]
    public Task<CheckInScheduleResponse> Set(Guid clientId, CheckInScheduleInput input, CancellationToken token) =>
        checkIns.SetScheduleAsync(clientId, input, token);

    [HttpDelete]
    public async Task<IActionResult> Delete(Guid clientId, CancellationToken token)
    {
        await checkIns.DeleteScheduleAsync(clientId, token);
        return NoContent();
    }
}
