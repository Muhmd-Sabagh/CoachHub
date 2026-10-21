using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's check-ins (§28, §29): the schedule and next due date, the coach's replies and review, and
/// a check-in to resubmit. Check-ins are submitted through <c>/me/forms</c>. Private notes are never included.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/check-ins")]
public sealed class MyCheckInsController(CheckInService checkIns) : ControllerBase
{
    [HttpGet]
    public Task<MyCheckInsResponse> List(CancellationToken token) => checkIns.MineAsync(ClientId(), token);

    [HttpGet("{id:guid}")]
    public Task<MyCheckInResponse> Get(Guid id, CancellationToken token) => checkIns.MyCheckInAsync(ClientId(), id, token);

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
