using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Settings;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/settings")]
public sealed class SettingsController(ICurrentUser currentUser, ICoachRepository coaches) : ControllerBase
{
    /// <summary>The coach name comes from the caller's coach (tenant); administrators have none.</summary>
    [HttpGet]
    [ProducesResponseType<SettingsResponse>(StatusCodes.Status200OK)]
    public async Task<SettingsResponse> Get(CancellationToken cancellationToken)
    {
        var coach = currentUser.CoachId is { } coachId
            ? await coaches.FindAsync(coachId, cancellationToken)
            : null;
        return new("CoachHub", coach?.DisplayName);
    }
}

public sealed record SettingsResponse(string ProductName, string? CoachName);
