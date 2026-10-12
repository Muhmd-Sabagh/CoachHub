using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.DietPlanning;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/diet-plans")]
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

    /// <summary>Version history, newest first (D4, §59).</summary>
    [HttpGet("{id:guid}/revisions")]
    public Task<IReadOnlyList<PlanRevisionInfo>> ListVersions(Guid id, CancellationToken cancellationToken) =>
        service.ListVersionsAsync(id, cancellationToken);

    /// <summary>One version; archived versions are read-only.</summary>
    [HttpGet("{id:guid}/revisions/{versionNumber:int:min(1)}")]
    public Task<DietPlanResponse> GetVersion(Guid id, int versionNumber, CancellationToken cancellationToken) =>
        service.GetVersionAsync(id, versionNumber, cancellationToken);

    /// <summary>Save in place: updates the current version.</summary>
    [HttpPut("{id:guid}/revisions/current")]
    public Task<DietPlanResponse> SaveInPlace(Guid id, DietPlanInput input, CancellationToken cancellationToken) =>
        service.SaveInPlaceAsync(id, input, cancellationToken);

    /// <summary>Save as new version: archives the current version and adds the next one.</summary>
    [HttpPost("{id:guid}/revisions")]
    public async Task<ActionResult<DietPlanResponse>> SaveAsNewVersion(Guid id, DietPlanInput input, CancellationToken cancellationToken)
    {
        var saved = await service.SaveAsNewVersionAsync(id, input, cancellationToken);
        return CreatedAtAction(nameof(GetVersion), new { id, versionNumber = saved.Revision.VersionNumber }, saved);
    }

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
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/nutrition-calculator")]
public sealed class NutritionCalculatorController(NutritionTargetService targets) : ControllerBase
{
    /// <summary>Calorie and macro targets (§14, §15) with the coach's coefficients.</summary>
    [HttpPost("energy")]
    public Task<EnergyCalculatorResponse> CalculateEnergy(EnergyCalculatorInput input, CancellationToken cancellationToken) =>
        targets.CalculateAsync(input, cancellationToken);
}
