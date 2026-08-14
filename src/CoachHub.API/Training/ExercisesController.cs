using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Training;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Training;

[ApiController]
[Authorize(Policy = AuthPermissions.ManageCatalog)]
[Route("api/training/exercises")]
public sealed class ExercisesController(
    ExerciseService exerciseService,
    LegacyExerciseImportService importService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ExerciseResponse>> List(
        [FromQuery] ExerciseQuery query,
        CancellationToken cancellationToken) =>
        exerciseService.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ExerciseResponse> Get(Guid id, CancellationToken cancellationToken) =>
        exerciseService.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ExerciseResponse>> Create(
        ExerciseInput input,
        CancellationToken cancellationToken)
    {
        var created = await exerciseService.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ExerciseResponse> Update(
        Guid id,
        ExerciseInput input,
        CancellationToken cancellationToken) =>
        exerciseService.UpdateAsync(id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await exerciseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("legacy-import")]
    public Task<LegacyExerciseImportResult> ImportLegacy(
        IReadOnlyCollection<LegacyExerciseImportRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count is 0 or > 5000)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["rows"] = ["Supply between 1 and 5000 legacy exercise rows."]
            });
        }
        return importService.ImportAsync(rows, cancellationToken);
    }
}