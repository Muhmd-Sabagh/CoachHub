using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Progress;
using CoachHub.Domain.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Progress;

/// <summary>
/// A client's progress as their coach sees it (§37, §40): the dashboard, body and lifestyle tracking, and photos.
/// Read-only except the client's daily targets, which the coach sets. Another coach's client is not found.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/progress")]
public sealed class ClientProgressController(ProgressService progress) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<ProgressDashboardResponse> Dashboard(Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        progress.DashboardAsync(clientId, from, to, token);

    [HttpGet("body")]
    public Task<BodyProgressResponse> Body(Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        progress.BodyAsync(clientId, from, to, token);

    [HttpGet("lifestyle")]
    public Task<LifestyleResponse> Lifestyle(Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        progress.LifestyleAsync(clientId, from, to, token);

    [HttpGet("photos")]
    public Task<IReadOnlyList<ProgressPhotoResponse>> Photos(Guid clientId, [FromQuery] PhotoPose? pose, CancellationToken token) =>
        progress.PhotosAsync(clientId, pose, token);

    [HttpGet("targets")]
    public Task<LifestyleTargetsResponse> Targets(Guid clientId, CancellationToken token) => progress.TargetsAsync(clientId, token);

    /// <summary>Sets the client's daily steps, water and sleep targets; a null value uses the coach's default.</summary>
    [HttpPut("targets")]
    public Task<LifestyleTargetsResponse> SetTargets(Guid clientId, LifestyleTargetValues input, CancellationToken token) =>
        progress.SetTargetsAsync(clientId, input, token);
}
