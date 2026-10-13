using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.FoodTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.FoodTracking;

/// <summary>
/// A client's nutrition as their coach sees it: a diary day, planned versus actual (§9), and analytics (§11).
/// Read-only; the client keeps the diary. Another coach's client is not found.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/nutrition")]
public sealed class ClientNutritionController(FoodTrackingService tracking) : ControllerBase
{
    [HttpGet("days/{date}")]
    public Task<NutritionDayResponse> Day(Guid clientId, DateOnly date, CancellationToken token) =>
        tracking.GetDayAsync(clientId, date, token);

    /// <summary>Each day, week (Monday first) and month from <paramref name="from"/> to <paramref name="to"/> (at most 366 days).</summary>
    [HttpGet("analytics")]
    public Task<NutritionAnalyticsResponse> Analytics(
        Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        tracking.AnalyticsAsync(clientId, from, to, token);
}
