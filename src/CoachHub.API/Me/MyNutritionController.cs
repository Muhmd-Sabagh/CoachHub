using CoachHub.Domain.Settings;
using CoachHub.API.Auth;
using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Nutrition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's food diary (§8) and nutrition dashboard (§10). The client always comes from the token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/nutrition")]
[ClientFeature(ClientFeature.Nutrition)]
public sealed class MyNutritionController(FoodTrackingService tracking, FoodService foods) : ControllerBase
{
    /// <summary>The day's plan, diary and target / consumed / remaining.</summary>
    [HttpGet("days/{date}")]
    public Task<NutritionDayResponse> Day(DateOnly date, CancellationToken token) =>
        tracking.GetDayAsync(ClientId(), date, token);

    /// <summary>Chooses the day's diet variant and flexible-meal options (§13).</summary>
    [HttpPut("days/{date}/choices")]
    public Task<NutritionDayResponse> Choose(DateOnly date, NutritionDayChoicesInput input, CancellationToken token) =>
        tracking.ChooseAsync(ClientId(), date, input, token);

    /// <summary>"Change Food" (§7): only the alternatives the coach permits, with their quantities.</summary>
    [HttpGet("days/{date}/foods/{plannedFoodId:guid}/alternatives")]
    public Task<IReadOnlyList<FoodAlternativeResponse>> Alternatives(DateOnly date, Guid plannedFoodId, CancellationToken token) =>
        tracking.AlternativesAsync(ClientId(), date, plannedFoodId, token);

    /// <summary>Logs a whole planned meal as eaten.</summary>
    [HttpPost("days/{date}/meals/{mealId:guid}/log")]
    public Task<NutritionDayResponse> LogMeal(DateOnly date, Guid mealId, CancellationToken token) =>
        tracking.LogMealAsync(ClientId(), date, mealId, token);

    [HttpPost("entries")]
    public async Task<ActionResult<FoodLogEntryResponse>> Log(FoodLogEntryInput input, CancellationToken token) =>
        StatusCode(StatusCodes.Status201Created, await tracking.LogAsync(ClientId(), input, token));

    [HttpPut("entries/{id:guid}")]
    public Task<FoodLogEntryResponse> Update(Guid id, FoodLogEntryUpdate input, CancellationToken token) =>
        tracking.UpdateAsync(ClientId(), id, input, token);

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await tracking.DeleteAsync(ClientId(), id, token);
        return NoContent();
    }

    /// <summary>Food search for the diary: active platform foods and the coach's custom foods.</summary>
    [HttpGet("foods")]
    public Task<PagedResult<FoodResponse>> Foods([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, CancellationToken token = default) =>
        foods.ListAsync(new FoodQuery { SearchTerm = searchTerm, PageNumber = pageNumber, PageSize = 20, IsActive = true }, token);

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
