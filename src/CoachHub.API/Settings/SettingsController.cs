using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Settings;

/// <summary>Platform settings (Administrator) and each coach's calculation defaults (§49).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AnyRole)]
[Route(ApiRoutes.V1 + "/settings")]
public sealed class SettingsController(SettingsService settings) : ControllerBase
{
    /// <summary>The brand name, coach name, units and default language for the signed-in user.</summary>
    [HttpGet]
    public Task<WorkspaceSettingsResponse> Get(CancellationToken cancellationToken) =>
        settings.GetWorkspaceAsync(cancellationToken);

    [HttpGet("platform")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<PlatformSettingsResponse> Platform(CancellationToken cancellationToken) =>
        settings.GetPlatformAsync(cancellationToken);

    [HttpPut("platform")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<PlatformSettingsResponse> UpdatePlatform(PlatformSettingsInput input, CancellationToken cancellationToken) =>
        settings.UpdatePlatformAsync(input, cancellationToken);

    /// <summary>The caller's coaching defaults; an Administrator passes <paramref name="coachId"/>.</summary>
    [HttpGet("coach")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    public Task<CoachSettingsResponse> Coach([FromQuery] Guid? coachId, CancellationToken cancellationToken) =>
        settings.GetCoachAsync(coachId, cancellationToken);

    [HttpPut("coach")]
    [Authorize(Policy = AuthPolicies.CoachOrAdmin)]
    public Task<CoachSettingsResponse> UpdateCoach(
        [FromQuery] Guid? coachId, CoachSettingsInput input, CancellationToken cancellationToken) =>
        settings.UpdateCoachAsync(coachId, input, cancellationToken);
}
