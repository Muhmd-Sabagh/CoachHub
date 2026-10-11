using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Reporting;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/reporting")]
public sealed class ReportingController(ReportingService service, AdvancedReportingService advanced) : ControllerBase
{
    [HttpGet("overview")]
    public Task<OperationalReport> Overview(
        [FromQuery] ReportingQuery query,
        CancellationToken cancellationToken) =>
        service.GetAsync(query, cancellationToken);

    [HttpGet("advanced")]
    public Task<AdvancedReport> Advanced([FromQuery] ReportingQuery query, CancellationToken cancellationToken) =>
        advanced.GetAsync(query, cancellationToken);
}