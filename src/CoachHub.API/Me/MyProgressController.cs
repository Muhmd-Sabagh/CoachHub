using CoachHub.Domain.Settings;
using System.Security.Claims;
using CoachHub.API.Auth;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Adherence;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Application.Progress;
using CoachHub.Domain.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's body (§24–§27) and lifestyle (§31–§33) tracking, photos and progress dashboard (§37).
/// The client always comes from the token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/progress")]
[ClientFeature(ClientFeature.Progress)]
public sealed class MyProgressController(ProgressService progress, ProgressDashboardService dashboard, AdherenceService adherence) : ControllerBase
{
    /// <summary>Body, nutrition, training and lifestyle for the range (at most 366 days).</summary>
    [HttpGet("dashboard")]
    public Task<ProgressDashboardResponse> Dashboard([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        dashboard.DashboardAsync(ClientId(), from, to, token);

    /// <summary>Adherence per area and overall (§30) for the range.</summary>
    [HttpGet("adherence")]
    public Task<AdherenceResponse> Adherence([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        adherence.ScoreAsync(ClientId(), from, to, token);

    /// <summary>Weight, body fat and measurements: history summaries, and entries within the range.</summary>
    [HttpGet("body")]
    public Task<BodyProgressResponse> Body([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        progress.BodyAsync(ClientId(), from, to, token);

    [HttpPut("weight/{date}")]
    public async Task<IActionResult> SetWeight(DateOnly date, WeightInput input, CancellationToken token)
    {
        await progress.SetWeightAsync(ClientId(), date, input, token);
        return NoContent();
    }

    [HttpDelete("weight/{date}")]
    public async Task<IActionResult> DeleteWeight(DateOnly date, CancellationToken token)
    {
        await progress.DeleteWeightAsync(ClientId(), date, token);
        return NoContent();
    }

    [HttpPut("body-fat/{date}")]
    public async Task<IActionResult> SetBodyFat(DateOnly date, BodyFatInput input, CancellationToken token)
    {
        await progress.SetBodyFatAsync(ClientId(), date, input, token);
        return NoContent();
    }

    [HttpDelete("body-fat/{date}")]
    public async Task<IActionResult> DeleteBodyFat(DateOnly date, CancellationToken token)
    {
        await progress.DeleteBodyFatAsync(ClientId(), date, token);
        return NoContent();
    }

    /// <summary>Sets the day's measurements; a site sent with a null value is removed.</summary>
    [HttpPut("measurements/{date}")]
    public async Task<IActionResult> SetMeasurements(DateOnly date, MeasurementsInput input, CancellationToken token)
    {
        await progress.SetMeasurementsAsync(ClientId(), date, input, token);
        return NoContent();
    }

    /// <summary>Sleep, steps and water for each day of the range, with targets and weekly figures.</summary>
    [HttpGet("lifestyle")]
    public Task<LifestyleResponse> Lifestyle([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        progress.LifestyleAsync(ClientId(), from, to, token);

    [HttpPut("sleep/{date}")]
    public async Task<IActionResult> SetSleep(DateOnly date, SleepInput input, CancellationToken token)
    {
        await progress.SetSleepAsync(ClientId(), date, input, token);
        return NoContent();
    }

    [HttpDelete("sleep/{date}")]
    public async Task<IActionResult> DeleteSleep(DateOnly date, CancellationToken token)
    {
        await progress.DeleteSleepAsync(ClientId(), date, token);
        return NoContent();
    }

    [HttpPut("steps/{date}")]
    public async Task<IActionResult> SetSteps(DateOnly date, StepsInput input, CancellationToken token)
    {
        await progress.SetStepsAsync(ClientId(), date, input, token);
        return NoContent();
    }

    [HttpDelete("steps/{date}")]
    public async Task<IActionResult> DeleteSteps(DateOnly date, CancellationToken token)
    {
        await progress.DeleteStepsAsync(ClientId(), date, token);
        return NoContent();
    }

    [HttpPost("water")]
    public async Task<ActionResult<WaterEntryResponse>> AddWater(WaterInput input, CancellationToken token) =>
        StatusCode(StatusCodes.Status201Created, await progress.AddWaterAsync(ClientId(), input, token));

    [HttpDelete("water/{id:guid}")]
    public async Task<IActionResult> DeleteWater(Guid id, CancellationToken token)
    {
        await progress.DeleteWaterAsync(ClientId(), id, token);
        return NoContent();
    }

    /// <summary>The daily targets the coach set (or the coach's defaults).</summary>
    [HttpGet("targets")]
    public Task<LifestyleTargetsResponse> Targets(CancellationToken token) => progress.TargetsAsync(ClientId(), token);

    [HttpGet("photos")]
    public Task<IReadOnlyList<ProgressPhotoResponse>> Photos([FromQuery] PhotoPose? pose, CancellationToken token) =>
        progress.PhotosAsync(ClientId(), pose, token);

    /// <summary>Uploads a progress photo; the image opens through <c>/media/{mediaId}</c> for the client and their coach only.</summary>
    [HttpPost("photos")]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<ActionResult<ProgressPhotoResponse>> AddPhoto(
        [FromForm] IFormFile file, [FromForm] DateOnly date, [FromForm] PhotoPose pose, [FromForm] string? label,
        [FromForm] string? notes, CancellationToken token)
    {
        await using var content = file.OpenReadStream();
        var photo = await progress.AddPhotoAsync(ClientId(), User.RequiredUserId(), new ProgressPhotoInput(date, pose, label, notes),
            content, file.FileName, file.ContentType, file.Length, token);
        return StatusCode(StatusCodes.Status201Created, photo);
    }

    [HttpPut("photos/{id:guid}")]
    public Task<ProgressPhotoResponse> ChangePhoto(Guid id, ProgressPhotoInput input, CancellationToken token) =>
        progress.ChangePhotoAsync(ClientId(), id, input, token);

    [HttpDelete("photos/{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken token)
    {
        await progress.DeletePhotoAsync(ClientId(), id, token);
        return NoContent();
    }

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
