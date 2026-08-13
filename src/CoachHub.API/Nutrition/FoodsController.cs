using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Nutrition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Nutrition;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/nutrition/foods")]
public sealed class FoodsController(
    FoodService foodService,
    LegacyFoodImportService importService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<FoodResponse>> List(
        [FromQuery] FoodQuery query,
        CancellationToken cancellationToken) =>
        foodService.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FoodResponse> Get(Guid id, CancellationToken cancellationToken) =>
        foodService.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FoodResponse>> Create(
        FoodInput input,
        CancellationToken cancellationToken)
    {
        var created = await foodService.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FoodResponse> Update(
        Guid id,
        FoodInput input,
        CancellationToken cancellationToken) =>
        foodService.UpdateAsync(id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await foodService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("legacy-import")]
    [ProducesResponseType<LegacyFoodImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<LegacyFoodImportResult> ImportLegacy(
        IReadOnlyCollection<LegacyFoodImportRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count is 0 or > 5000)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["rows"] = ["Supply between 1 and 5000 legacy food rows."]
            });
        }

        return importService.ImportAsync(rows, cancellationToken);
    }
}