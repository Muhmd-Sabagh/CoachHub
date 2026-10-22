using CoachHub.Domain.Settings;
using CoachHub.API.Auth;
using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutTracking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's workouts (§18), history (§19), PRs (§21) and training analytics (§20, §23).
/// The client always comes from the token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/training")]
[ClientFeature(ClientFeature.Training)]
public sealed class MyTrainingController(WorkoutTrackingService tracking, ExerciseService exercises) : ControllerBase
{
    /// <summary>"Today's Workout": the workout in progress, the plan's rotation with the suggested day, and the week.</summary>
    [HttpGet("days/{date}")]
    public Task<TrainingDayResponse> Day(DateOnly date, CancellationToken token) =>
        tracking.GetDayAsync(ClientId(), date, token);

    [HttpGet("sessions")]
    public Task<PagedResult<WorkoutSessionSummary>> Sessions([FromQuery] WorkoutSessionQuery query, CancellationToken token) =>
        tracking.ListSessionsAsync(ClientId(), query, token);

    /// <summary>Starts a planned day or a free workout; 409 while another workout is in progress.</summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<WorkoutSessionResponse>> Start(StartWorkoutInput input, CancellationToken token) =>
        StatusCode(StatusCodes.Status201Created, await tracking.StartAsync(ClientId(), input, token));

    [HttpGet("sessions/{id:guid}")]
    public Task<WorkoutSessionResponse> Session(Guid id, CancellationToken token) =>
        tracking.GetSessionAsync(ClientId(), id, token);

    [HttpPut("sessions/{id:guid}/notes")]
    public Task<WorkoutSessionResponse> Notes(Guid id, WorkoutNotesInput input, CancellationToken token) =>
        tracking.ChangeNotesAsync(ClientId(), id, input, token);

    /// <summary>Completes the workout; the response lists the PRs it set.</summary>
    [HttpPost("sessions/{id:guid}/complete")]
    public Task<WorkoutSessionResponse> Complete(Guid id, WorkoutNotesInput? input, CancellationToken token) =>
        tracking.CompleteAsync(ClientId(), id, input, token);

    /// <summary>Discards a workout in progress, or deletes a completed one.</summary>
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await tracking.DeleteSessionAsync(ClientId(), id, token);
        return NoContent();
    }

    /// <summary>Adds an exercise from the library to the workout.</summary>
    [HttpPost("sessions/{id:guid}/exercises")]
    public Task<WorkoutSessionResponse> AddExercise(Guid id, AddLoggedExerciseInput input, CancellationToken token) =>
        tracking.AddExerciseAsync(ClientId(), id, input, token);

    /// <summary>Removes an exercise the client added.</summary>
    [HttpDelete("sessions/{id:guid}/exercises/{exerciseId:guid}")]
    public Task<WorkoutSessionResponse> RemoveExercise(Guid id, Guid exerciseId, CancellationToken token) =>
        tracking.RemoveExerciseAsync(ClientId(), id, exerciseId, token);

    /// <summary>Skips a planned exercise when the coach permits it, or resumes it.</summary>
    [HttpPut("sessions/{id:guid}/exercises/{exerciseId:guid}/skip")]
    public Task<WorkoutSessionResponse> Skip(Guid id, Guid exerciseId, SkipLoggedExerciseInput input, CancellationToken token) =>
        tracking.SkipExerciseAsync(ClientId(), id, exerciseId, input, token);

    [HttpPut("sessions/{id:guid}/exercises/{exerciseId:guid}/notes")]
    public Task<WorkoutSessionResponse> ExerciseNotes(Guid id, Guid exerciseId, LoggedExerciseNotesInput input, CancellationToken token) =>
        tracking.ChangeExerciseNotesAsync(ClientId(), id, exerciseId, input, token);

    [HttpPost("sessions/{id:guid}/exercises/{exerciseId:guid}/sets")]
    public Task<WorkoutSessionResponse> LogSet(Guid id, Guid exerciseId, LoggedSetInput input, CancellationToken token) =>
        tracking.LogSetAsync(ClientId(), id, exerciseId, input, token);

    [HttpPut("sessions/{id:guid}/sets/{setId:guid}")]
    public Task<WorkoutSessionResponse> UpdateSet(Guid id, Guid setId, LoggedSetInput input, CancellationToken token) =>
        tracking.UpdateSetAsync(ClientId(), id, setId, input, token);

    [HttpDelete("sessions/{id:guid}/sets/{setId:guid}")]
    public Task<WorkoutSessionResponse> DeleteSet(Guid id, Guid setId, CancellationToken token) =>
        tracking.DeleteSetAsync(ClientId(), id, setId, token);

    [HttpGet("exercises/{exerciseId:guid}/history")]
    public Task<ExerciseHistoryResponse> History(Guid exerciseId, CancellationToken token) =>
        tracking.HistoryAsync(ClientId(), exerciseId, token);

    [HttpGet("records")]
    public Task<PersonalRecordsResponse> Records(CancellationToken token) =>
        tracking.RecordsAsync(ClientId(), token);

    /// <summary>Totals, weeks, months and exercise progression from <paramref name="from"/> to <paramref name="to"/> (at most 366 days).</summary>
    [HttpGet("analytics")]
    public Task<TrainingAnalyticsResponse> Analytics([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        tracking.AnalyticsAsync(ClientId(), from, to, token);

    /// <summary>Exercise search for adding to a workout: active platform exercises and the coach's own.</summary>
    [HttpGet("exercises")]
    public Task<PagedResult<ExerciseResponse>> Exercises([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, CancellationToken token = default) =>
        exercises.ListAsync(new ExerciseQuery { SearchTerm = searchTerm, PageNumber = pageNumber, PageSize = 20, IsActive = true }, token);

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
