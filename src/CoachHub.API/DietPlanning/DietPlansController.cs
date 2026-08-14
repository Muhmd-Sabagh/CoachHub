using CoachHub.Application.Auth;
using CoachHub.Application.DietPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.DietPlanning;

[ApiController]
[Authorize(Policy = AuthPermissions.ManagePlans)]
[Route("api/diet-plans")]
public sealed class DietPlansController(DietPlanService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<DietPlanResponse> Get(Guid id, CancellationToken cancellationToken) => service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<DietPlanResponse>> Create(DietPlanInput input, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<DietPlanResponse> Update(Guid id, DietPlanInput input, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, input, cancellationToken);

    [HttpPost("{id:guid}/copies")]
    public async Task<ActionResult<DietPlanResponse>> Copy(Guid id, CopyDietPlanInput input, CancellationToken cancellationToken)
    {
        var created = await service.CopyAsync(id, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/assignment")]
    public Task<DietPlanResponse> Assign(Guid id, AssignDietPlanInput input, CancellationToken cancellationToken) =>
        service.AssignAsync(id, input.ClientId, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/notes/{noteId:guid}/active")]
    public Task<DietPlanResponse> SetNoteActive(Guid id, Guid noteId, SetDietPlanNoteActiveInput input, CancellationToken cancellationToken) =>
        service.SetNoteActiveAsync(id, noteId, input.IsActive, cancellationToken);
}

[ApiController]
[Authorize(Policy = AuthPermissions.ManagePlans)]
[Route("api/nutrition-calculator")]
public sealed class NutritionCalculatorController : ControllerBase
{
    [HttpPost("energy")]
    public EnergyCalculatorResponse CalculateEnergy(EnergyCalculatorInput input) => NutritionCalculator.CalculateEnergy(input);
}
