using CoachHub.Application.Auth;
using CoachHub.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Media;

[ApiController]
[Authorize(Policy = AuthPermissions.ManageMedia)]
[Route("api/media")]
public sealed class MediaController(MediaService mediaService) : ControllerBase
{
    [HttpPost]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediaService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
