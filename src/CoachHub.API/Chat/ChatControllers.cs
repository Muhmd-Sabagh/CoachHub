using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Chat;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Domain.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Chat;

/// <summary>The coach's conversations with their clients (§41), most recent first.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/conversations")]
public sealed class ConversationsController(ChatService chat) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ConversationSummary>> List([FromQuery] ConversationQuery query, CancellationToken token) =>
        chat.ConversationsAsync(query, token);
}

/// <summary>
/// The chat with one client, as their coach (§41). Administrators may read but not write. Another coach's client is
/// not found; an archived conversation (a previous coach's) is never shown here.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/chat")]
public sealed class ClientChatController(ChatService chat) : ControllerBase
{
    [HttpGet]
    public Task<ChatThreadResponse> Thread(Guid clientId, [FromQuery] int pageSize = 30, CancellationToken token = default) =>
        chat.ThreadAsync(clientId, ChatParticipant.Coach, pageSize, token);

    /// <summary>Older messages, newest first, before the given time.</summary>
    [HttpGet("messages")]
    public Task<ChatMessagesPage> Messages(Guid clientId, [FromQuery] DateTimeOffset? before, [FromQuery] int pageSize = 30, CancellationToken token = default) =>
        chat.MessagesAsync(clientId, null, before, pageSize, ChatParticipant.Coach, token);

    [HttpPost("messages")]
    public Task<ChatMessageResponse> Send(Guid clientId, ChatSendInput input, CancellationToken token) =>
        chat.SendAsync(clientId, input, ChatParticipant.Coach, token);

    /// <summary>Uploads a file to attach (images, PDF, video, Office documents, text; up to 50 MB).</summary>
    [HttpPost("attachments")]
    [RequestSizeLimit(MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.MaximumSharedFileSizeBytes + 1024 * 1024)]
    public async Task<MediaMetadata> Upload(Guid clientId, [FromForm] IFormFile file, CancellationToken token)
    {
        await using var content = file.OpenReadStream();
        return await chat.UploadAsync(clientId, content, file.FileName, file.ContentType, file.Length, ChatParticipant.Coach, token);
    }

    [HttpPost("read")]
    public async Task<IActionResult> Read(Guid clientId, CancellationToken token)
    {
        await chat.MarkReadAsync(clientId, ChatParticipant.Coach, token);
        return NoContent();
    }
}
