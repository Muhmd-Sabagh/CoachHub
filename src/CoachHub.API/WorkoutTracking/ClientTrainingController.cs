using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.WorkoutTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.WorkoutTracking;

/// <summary>
/// A client's training as their coach sees it: workouts (§18), exercise history (§19), PRs (§21) and analytics
/// (§20, §23). Read-only; the client logs their workouts. Another coach's client is not found.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/training")]
public sealed class ClientTrainingController(WorkoutTrackingService tracking) : ControllerBase
{
    [HttpGet("days/{date}")]
    public Task<TrainingDayResponse> Day(Guid clientId, DateOnly date, CancellationToken token) =>
        tracking.GetDayAsync(clientId, date, token);

    [HttpGet("sessions")]
    public Task<PagedResult<WorkoutSessionSummary>> Sessions(Guid clientId, [FromQuery] WorkoutSessionQuery query, CancellationToken token) =>
        tracking.ListSessionsAsync(clientId, query, token);

    [HttpGet("sessions/{id:guid}")]
    public Task<WorkoutSessionResponse> Session(Guid clientId, Guid id, CancellationToken token) =>
        tracking.GetSessionAsync(clientId, id, token);

    [HttpGet("exercises/{exerciseId:guid}/history")]
    public Task<ExerciseHistoryResponse> History(Guid clientId, Guid exerciseId, CancellationToken token) =>
        tracking.HistoryAsync(clientId, exerciseId, token);

    [HttpGet("records")]
    public Task<PersonalRecordsResponse> Records(Guid clientId, CancellationToken token) =>
        tracking.RecordsAsync(clientId, token);

    /// <summary>Totals, weeks (Monday first), months and exercise progression (at most 366 days).</summary>
    [HttpGet("analytics")]
    public Task<TrainingAnalyticsResponse> Analytics(
        Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        tracking.AnalyticsAsync(clientId, from, to, token);
}
