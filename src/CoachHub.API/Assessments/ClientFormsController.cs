using CoachHub.Application.Assessments;
using CoachHub.Application.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoachHub.API.Assessments;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[EnableRateLimiting("client-forms")]
[Route("api/client-forms")]
public sealed class ClientFormsController(FormSubmissionService service, MediaService mediaService) : ControllerBase
{
    [HttpPost("access/validate")]
    public Task<FormAccessResponse> Validate(FormAccessInput input, CancellationToken token) =>
        service.ValidateAccessAsync(input, token);

    [HttpPost("{definitionId:guid}/questions")]
    public Task<FormVersionResponse> Questions(
        Guid definitionId, FormAccessInput input, CancellationToken token) =>
        service.GetPublishedAsync(input, definitionId, token);

    [HttpPost("submissions")]
    public Task<SubmissionResponse> Submit(SubmitFormInput input, CancellationToken token) =>
        service.SubmitAsync(input, token);

    [HttpPost("media")]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<ActionResult<MediaMetadata>> UploadMedia(
        [FromForm] string clientCode,
        [FromForm] string formCode,
        [FromForm] IFormFile file,
        CancellationToken token)
    {
        await service.ValidateAccessAsync(new(clientCode, formCode), token);
        await using var content = file.OpenReadStream();
        var media = await mediaService.UploadAsync(
            content, file.FileName, file.ContentType, file.Length, token);
        return StatusCode(StatusCodes.Status201Created, media);
    }
}