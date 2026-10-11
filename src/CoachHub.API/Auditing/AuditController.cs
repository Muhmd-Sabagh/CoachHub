using CoachHub.API.Common.Routing;
using CoachHub.Application.Auditing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auditing;

[ApiController]
[Authorize(Policy = AuthPermissions.ViewAudit)]
[Route(ApiRoutes.V1 + "/audit-entries")]
public sealed class AuditController(AuditQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditRecord>> List(
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);
}
