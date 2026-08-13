using CoachHub.Application.Auditing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auditing;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/audit-entries")]
public sealed class AuditController(AuditQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditRecord>> List(
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);
}
