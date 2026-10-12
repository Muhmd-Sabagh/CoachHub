using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.WorkoutPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.WorkoutPlanning;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/workout-plans")]
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

    /// <summary>Version history, newest first (D4, §59).</summary>
    [HttpGet("{id:guid}/revisions")]
    public Task<IReadOnlyList<PlanRevisionInfo>> ListVersions(Guid id, CancellationToken cancellationToken) =>
        service.ListVersionsAsync(id, cancellationToken);

    /// <summary>One version; archived versions are read-only.</summary>
    [HttpGet("{id:guid}/revisions/{versionNumber:int:min(1)}")]
    public Task<WorkoutPlanResponse> GetVersion(Guid id, int versionNumber, CancellationToken cancellationToken) =>
        service.GetVersionAsync(id, versionNumber, cancellationToken);

    /// <summary>Save in place: updates the current version.</summary>
    [HttpPut("{id:guid}/revisions/current")]
    public Task<WorkoutPlanResponse> SaveInPlace(Guid id, WorkoutPlanInput input, CancellationToken cancellationToken) =>
        service.SaveInPlaceAsync(id, input, cancellationToken);

    /// <summary>Save as new version: archives the current version and adds the next one.</summary>
    [HttpPost("{id:guid}/revisions")]
    public async Task<ActionResult<WorkoutPlanResponse>> SaveAsNewVersion(Guid id, WorkoutPlanInput input, CancellationToken cancellationToken)
    {
        var saved = await service.SaveAsNewVersionAsync(id, input, cancellationToken);
        return CreatedAtAction(nameof(GetVersion), new { id, versionNumber = saved.Revision.VersionNumber }, saved);
    }

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
