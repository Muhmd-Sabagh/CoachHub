using System.Security.Claims;
using CoachHub.API.Auth;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// Assessments and check-in forms for the signed-in client (D5). They replace the retired
/// code-based public forms: the client comes from the token, never from a code or an ID.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/forms")]
public sealed class MyFormsController(FormSubmissionService forms, MediaService media) : ControllerBase
{
    [HttpGet]
    public Task<MyFormsResponse> List(CancellationToken token) => forms.ListForClientAsync(ClientId(), token);

    [HttpGet("{definitionId:guid}")]
    public Task<FormVersionResponse> Get(Guid definitionId, CancellationToken token) =>
        forms.GetForClientAsync(ClientId(), definitionId, token);

    [HttpPost("{definitionId:guid}/submissions")]
    public async Task<ActionResult<SubmissionResponse>> Submit(
        Guid definitionId, MyFormSubmissionInput input, CancellationToken token) =>
        StatusCode(StatusCodes.Status201Created, await forms.SubmitForClientAsync(ClientId(), definitionId, input, token));

    /// <summary>A photo or file for a media answer; it belongs to the client.</summary>
    [HttpPost("media")]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<ActionResult<MediaMetadata>> UploadMedia([FromForm] IFormFile file, CancellationToken token)
    {
        var owner = await forms.UploadOwnerAsync(ClientId(), User.RequiredUserId(), token);
        await using var content = file.OpenReadStream();
        var stored = await media.UploadAsync(content, file.FileName, file.ContentType, file.Length, owner, token);
        return StatusCode(StatusCodes.Status201Created, stored);
    }

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}
