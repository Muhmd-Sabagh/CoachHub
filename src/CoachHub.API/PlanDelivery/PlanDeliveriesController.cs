using CoachHub.Application.Auth;
using CoachHub.Application.PlanDelivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.PlanDelivery;

[ApiController]
[Route("api/plan-deliveries")]
[Authorize(Policy = AuthPermissions.ManagePlans)]
public sealed class PlanDeliveriesController(PlanDeliveryService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<DeliveredPlanResponse>> List([FromQuery] Guid clientId, CancellationToken token) => service.ListAsync(clientId, token);
    [HttpPost] public async Task<ActionResult<DeliveredPlanResponse>> Deliver(DeliveredPlanInput input, CancellationToken token) { var created = await service.DeliverAsync(input, token); return Created($"/api/plan-deliveries/{created.Id}", created); }
}
