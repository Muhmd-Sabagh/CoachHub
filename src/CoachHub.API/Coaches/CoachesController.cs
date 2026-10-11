using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Coaches;

/// <summary>Administrator management of coaches, the platform's tenants (§2 Admin, §56).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route(ApiRoutes.V1 + "/admin/coaches")]
public sealed class CoachesController(ICoachAdministrationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CoachResponse>> List([FromQuery] CoachQuery query, CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CoachResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CoachResponse>> Create(CoachCreateInput input, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<CoachResponse> Update(Guid id, CoachUpdateInput input, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, input, cancellationToken);
}
