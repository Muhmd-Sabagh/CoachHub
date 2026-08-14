using CoachHub.Application.Auth;
using CoachHub.Application.WorkoutPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.WorkoutPlanning;

[ApiController]
[Authorize(Policy = AuthPermissions.ManagePlans)]
[Route("api/workout-plans")]
public sealed class WorkoutPlansController(WorkoutPlanService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<WorkoutPlanResponse> Get(Guid id, CancellationToken cancellationToken) => service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<WorkoutPlanResponse>> Create(WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<WorkoutPlanResponse> Update(Guid id, WorkoutPlanInput input, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, input, cancellationToken);

    [HttpPost("{id:guid}/copies")]
    public async Task<ActionResult<WorkoutPlanResponse>> Copy(
        Guid id, CopyWorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var created = await service.CopyAsync(id, input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/assignment")]
    public Task<WorkoutPlanResponse> Assign(Guid id, AssignWorkoutPlanInput input, CancellationToken cancellationToken) =>
        service.AssignAsync(id, input.ClientId, cancellationToken);

    [HttpPut("{id:guid}/notes/{noteId:guid}/active")]
    public Task<WorkoutPlanResponse> SetNoteActive(
        Guid id, Guid noteId, SetWorkoutPlanNoteActiveInput input, CancellationToken cancellationToken) =>
        service.SetNoteActiveAsync(id, noteId, input.IsActive, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken); return NoContent();
    }
}
