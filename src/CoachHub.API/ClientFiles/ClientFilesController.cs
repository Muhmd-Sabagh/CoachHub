using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.ClientFiles;
using CoachHub.Application.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.ClientFiles;

/// <summary>
/// Files the coach shares with a client (§43): PDFs, images, videos and documents up to 50 MB. Another coach's client
/// is not found.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/files")]
public sealed class ClientFilesController(ClientFileService files) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientFileResponse>> List(Guid clientId, CancellationToken token) => files.ListAsync(clientId, token);

    [HttpPost]
    [RequestSizeLimit(MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    public async Task<ClientFileResponse> Share(
        Guid clientId, [FromForm] IFormFile file, [FromForm] string? title, [FromForm] string? description, CancellationToken token)
    {
        await using var content = file.OpenReadStream();
        return await files.ShareAsync(clientId, content, file.FileName, file.ContentType, file.Length,
            new ClientFileInput(title ?? string.Empty, description), token);
    }

    [HttpPut("{fileId:guid}")]
    public Task<ClientFileResponse> Update(Guid clientId, Guid fileId, ClientFileInput input, CancellationToken token) =>
        files.UpdateAsync(clientId, fileId, input, token);

    [HttpDelete("{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid clientId, Guid fileId, CancellationToken token)
    {
        await files.DeleteAsync(clientId, fileId, token);
        return NoContent();
    }
}
