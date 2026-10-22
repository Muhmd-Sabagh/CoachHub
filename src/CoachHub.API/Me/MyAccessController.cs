using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's subscription status and what each part of the client area allows (§57), so the app can
/// say why something is read-only. Never restricted itself.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/access")]
public sealed class MyAccessController(SubscriptionAccessService access) : ControllerBase
{
    [HttpGet]
    public Task<MyAccessResponse> Get(CancellationToken token)
    {
        var clientId = Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
        return access.MineAsync(clientId, token);
    }
}
