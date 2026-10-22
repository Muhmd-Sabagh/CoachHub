using CoachHub.Domain.Settings;
using CoachHub.API.Auth;
using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Chat;
using CoachHub.Application.ClientFiles;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in client's chat with their coach (§41), and earlier conversations with previous coaches, read-only.
/// The client always comes from the token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/chat")]
[ClientFeature(ClientFeature.Chat)]
public sealed class MyChatController(ChatService chat) : ControllerBase
{
    [HttpGet]
    public Task<MyChatResponse> Get([FromQuery] int pageSize = 30, CancellationToken token = default) =>
        chat.MineAsync(ClientId(), pageSize, token);

    /// <summary>Older messages of the open conversation, or of an archived one named by <paramref name="conversationId"/>.</summary>
    [HttpGet("messages")]
    public Task<ChatMessagesPage> Messages(
        [FromQuery] Guid? conversationId, [FromQuery] DateTimeOffset? before, [FromQuery] int pageSize = 30, CancellationToken token = default) =>
        chat.MessagesAsync(ClientId(), conversationId, before, pageSize, ChatParticipant.Client, token);

    [HttpPost("messages")]
    public Task<ChatMessageResponse> Send(ChatSendInput input, CancellationToken token) =>
        chat.SendAsync(ClientId(), input, ChatParticipant.Client, token);

    [HttpPost("attachments")]
    [RequestSizeLimit(MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    public async Task<MediaMetadata> Upload([FromForm] IFormFile file, CancellationToken token)
    {
        await using var content = file.OpenReadStream();
        return await chat.UploadAsync(ClientId(), content, file.FileName, file.ContentType, file.Length, ChatParticipant.Client, token);
    }

    [HttpPost("read")]
    public async Task<IActionResult> Read(CancellationToken token)
    {
        await chat.MarkReadAsync(ClientId(), ChatParticipant.Client, token);
        return NoContent();
    }

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}

/// <summary>The files the coach shared with the signed-in client (§43); each opens through <c>/media/{mediaId}</c>.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/files")]
[ClientFeature(ClientFeature.Files)]
public sealed class MyFilesController(ClientFileService files) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientFileResponse>> List(CancellationToken token)
    {
        var clientId = Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
        return files.ListAsync(clientId, token);
    }
}
