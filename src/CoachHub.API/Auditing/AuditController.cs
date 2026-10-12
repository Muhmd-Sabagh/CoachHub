using CoachHub.API.Common.Routing;
using CoachHub.Application.Auditing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auditing;

/// <summary>
/// The audit trail (§58). Administrators review everything; a coach reads their own tenant's
/// entries only (tenant filter on AuditEntry).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/audit-entries")]
public sealed class AuditController(AuditQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditRecord>> List(
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);
}
