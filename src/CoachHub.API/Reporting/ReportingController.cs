using CoachHub.Application.Auth;
using CoachHub.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Reporting;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/reporting")]
public sealed class ReportingController(ReportingService service) : ControllerBase
{
    [HttpGet("overview")]
    public Task<OperationalReport> Overview(
        [FromQuery] ReportingQuery query,
        CancellationToken cancellationToken) =>
        service.GetAsync(query, cancellationToken);
}