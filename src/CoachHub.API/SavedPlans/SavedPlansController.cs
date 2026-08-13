using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.SavedPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.SavedPlans;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/saved-plans")]
public sealed class SavedPlansController(SavedPlanQueryService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SavedPlanSummary>> List(
        [FromQuery] SavedPlanQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);
}
