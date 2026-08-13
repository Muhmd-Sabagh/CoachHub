using CoachHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CoachHub.API.Settings;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/settings")]
public sealed class SettingsController(IOptions<CoachHubOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SettingsResponse>(StatusCodes.Status200OK)]
    public SettingsResponse Get() => new("CoachHub", options.Value.CoachName);
}

public sealed record SettingsResponse(string ProductName, string CoachName);