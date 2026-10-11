using CoachHub.API.Auth;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Media;

/// <summary>
/// Media is served only here. Who may open a file is decided by its owner (platform, coach
/// library, or client media; see MediaAsset), enforced by the tenant filters: others get 404.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AnyRole)]
[Route(ApiRoutes.V1 + "/media")]
public sealed class MediaController(MediaService mediaService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    [ProducesResponseType<MediaMetadata>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MediaMetadata>> Upload(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var media = await mediaService.UploadAsync(
            content,
            file.FileName,
            file.ContentType,
            file.Length,
            MediaOwner.Platform(User.RequiredUserId()),
            cancellationToken);

        return CreatedAtAction(nameof(Open), new { id = media.Id }, media);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        var media = await mediaService.OpenReadAsync(id, cancellationToken);
        return File(media.Content, media.ContentType, media.FileName);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediaService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
