using CoachHub.Application.Auth;
using CoachHub.Application.Common.Events;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Application.Notifications;
using CoachHub.Domain.Chat;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Chat;

/// <summary>
/// Coach ↔ client chat (§41): text and files, history paging, read status, live delivery and notifications. The coach
/// works under <c>/clients/{id}/chat</c>; the client under <c>/me/chat</c> with the client from the token. Tenant
/// filters decide who sees which client; administrators may read but not write.
/// </summary>
public sealed class ChatService(
    IChatRepository repository,
    IMediaRepository mediaRepository,
    MediaService media,
    IRecipientDirectory directory,
    IRealtimePublisher realtime,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const int MaximumPageSize = 50;

    public Task<PagedResult<ConversationSummary>> ConversationsAsync(ConversationQuery query, CancellationToken cancellationToken) =>
        repository.ListForCoachAsync(query.Normalize(), cancellationToken);

    /// <summary>The open conversation with the client and its latest messages.</summary>
    public async Task<ChatThreadResponse> ThreadAsync(Guid clientId, ChatParticipant reader, int pageSize, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(clientId, cancellationToken);
        var conversation = await repository.FindOpenAsync(clientId, cancellationToken);
        if (conversation is null) return new ChatThreadResponse(client.Id, client.Name, null, 0, new ChatMessagesPage([], false));
        return new ChatThreadResponse(client.Id, client.Name, conversation.Id,
            await repository.UnreadCountAsync(conversation.Id, reader, cancellationToken),
            await PageAsync(conversation.Id, null, pageSize, cancellationToken));
    }

    /// <summary>The client's open conversation and the archived ones with previous coaches.</summary>
    public async Task<MyChatResponse> MineAsync(Guid clientId, int pageSize, CancellationToken cancellationToken)
    {
        var current = await ThreadAsync(clientId, ChatParticipant.Client, pageSize, cancellationToken);
        var archived = await repository.ListArchivedAsync(clientId, cancellationToken);
        return new MyChatResponse(current, archived
            .Select(x => new ArchivedConversationResponse(x.Id, x.ArchivedCoachName ?? "Previous coach", x.ArchivedAt!.Value, x.LastMessageAt))
            .ToArray());
    }

    /// <summary>
    /// Older messages of a conversation. Coaches and administrators read only the open conversation; the client also reads their archived
    /// ones (owner decision 2026-10-22).
    /// </summary>
    public async Task<ChatMessagesPage> MessagesAsync(
        Guid clientId, Guid? conversationId, DateTimeOffset? before, int pageSize, ChatParticipant reader, CancellationToken cancellationToken)
    {
        await ClientAsync(clientId, cancellationToken);
        var conversation = conversationId is { } id
            ? await repository.FindAsync(clientId, id, cancellationToken)
            : await repository.FindOpenAsync(clientId, cancellationToken);
        if (conversation is null || (conversation.IsArchived && reader == ChatParticipant.Coach))
        {
            if (conversationId is null) return new ChatMessagesPage([], false);
            throw new NotFoundException("Conversation", conversationId.Value);
        }
        return await PageAsync(conversation.Id, before, pageSize, cancellationToken);
    }

    public async Task<ChatMessageResponse> SendAsync(Guid clientId, ChatSendInput input, ChatParticipant sender, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var userId = Writer(sender);
        var client = await ClientAsync(clientId, cancellationToken);
        var attachments = new List<ChatAttachmentInput>();
        foreach (var mediaId in (input.MediaIds ?? []).Distinct())
        {
            var file = await mediaRepository.FindAsync(mediaId, cancellationToken);
            // Only files the sender uploaded for this client's chat.
            if (file is null || file.ClientId != clientId || file.OwnerUserId != userId)
                throw Validation("mediaIds", "Attach files you uploaded to this conversation.");
            attachments.Add(new ChatAttachmentInput(file.Id, file.OriginalFileName, file.ContentType, file.SizeBytes));
        }
        var now = timeProvider.GetUtcNow();
        var conversation = await repository.FindOpenAsync(clientId, cancellationToken);
        if (conversation is null)
        {
            conversation = Conversation.Start(client.CoachId, clientId, now);
            repository.Add(conversation);
        }
        var message = DomainValidation.Run(() => ChatMessage.Send(conversation, sender, userId, input.Text, attachments, now), "text");
        repository.Add(message);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(message);
    }

    /// <summary>Uploads a file to attach; it becomes the client's media, visible to the client and the coach.</summary>
    public async Task<MediaMetadata> UploadAsync(
        Guid clientId, Stream content, string fileName, string contentType, long sizeBytes, ChatParticipant sender, CancellationToken cancellationToken)
    {
        var userId = Writer(sender);
        var client = await ClientAsync(clientId, cancellationToken);
        return await media.UploadAsync(content, fileName, contentType, sizeBytes,
            MediaOwner.OfClient(client.CoachId, clientId, userId), MediaUploadPolicy.Shared, cancellationToken);
    }

    /// <summary>Marks the other side's messages in the open conversation read, and tells them live.</summary>
    public async Task MarkReadAsync(Guid clientId, ChatParticipant reader, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(clientId, cancellationToken);
        var conversation = await repository.FindOpenAsync(clientId, cancellationToken);
        if (conversation is null) return;
        var unread = await repository.UnreadAsync(conversation.Id, reader, cancellationToken);
        if (unread.Count == 0) return;
        var now = timeProvider.GetUtcNow();
        foreach (var message in unread) message.MarkRead(now);
        await repository.SaveChangesAsync(cancellationToken);
        var others = reader == ChatParticipant.Client
            ? await directory.CoachAsync(client.CoachId, cancellationToken)
            : await directory.ClientAsync(clientId, cancellationToken);
        await realtime.PublishAsync(others.Select(x => x.UserId).ToArray(), RealtimeEvents.ChatRead,
            new ChatReadResponse(conversation.Id, reader, now), cancellationToken);
    }

    internal static ChatMessageResponse Map(ChatMessage message) => new(
        message.Id, message.ConversationId, message.ClientId, message.Sender, message.Text, message.SentAt, message.ReadAt,
        message.Attachments.Select(x => new ChatAttachmentResponse(x.MediaId, x.FileName, x.ContentType, x.SizeBytes)).ToArray());

    private async Task<ChatMessagesPage> PageAsync(Guid conversationId, DateTimeOffset? before, int pageSize, CancellationToken cancellationToken)
    {
        var (items, hasMore) = await repository.MessagesAsync(conversationId, before, Math.Clamp(pageSize, 1, MaximumPageSize), cancellationToken);
        return new ChatMessagesPage(items.Select(Map).ToArray(), hasMore);
    }

    /// <summary>The sending account: the client themselves, or the coach (administrators only read).</summary>
    private Guid Writer(ChatParticipant sender)
    {
        if (sender == ChatParticipant.Coach && currentUser.Role != AuthRoles.Coach)
            throw new ForbiddenException("Only the client's coach can write in the conversation.");
        return currentUser.UserId ?? throw new ForbiddenException("Sign in to chat.");
    }

    private async Task<ChatClient> ClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>
/// A message was sent (§41, §42): both sides receive it live, and the other side gets a "new message" notification,
/// one per conversation until they read it.
/// </summary>
public sealed class ChatMessageSentHandler(
    IChatRepository repository,
    IRecipientDirectory directory,
    IRealtimePublisher realtime,
    NotificationCenter center) : IDomainEventHandler<ChatMessageSent>
{
    public async Task HandleAsync(ChatMessageSent domainEvent, CancellationToken cancellationToken)
    {
        var message = await repository.FindMessageAsync(domainEvent.MessageId, cancellationToken);
        if (message is null) return;
        var coaches = await directory.CoachAsync(domainEvent.CoachId, cancellationToken);
        var clients = await directory.ClientAsync(domainEvent.ClientId, cancellationToken);
        await realtime.PublishAsync(coaches.Concat(clients).Select(x => x.UserId).Distinct().ToArray(),
            RealtimeEvents.ChatMessage, ChatService.Map(message), cancellationToken);

        var preview = message.Text is { } text
            ? NoticeText.Verbatim(text.Length > 140 ? text[..140] + "…" : text)
            : NoticeText.Of("notice.sentFile");
        var key = $"chat:{domainEvent.ConversationId}";
        if (domainEvent.Sender == ChatParticipant.Coach)
        {
            await center.NotifyClientAsync(new EventNotice(NotificationEvent.NewMessage, domainEvent.CoachId, domainEvent.ClientId,
                NoticeText.Of("notice.messageFromCoach.title"), preview, "/app/chat", key, OnlyWhileUnread: true), cancellationToken);
        }
        else
        {
            var name = await directory.ClientNameAsync(domainEvent.ClientId, cancellationToken) is { } clientName
                ? (object)clientName
                : NoticeText.Of("notice.yourClient");
            await center.NotifyCoachAsync(new EventNotice(NotificationEvent.NewMessage, domainEvent.CoachId, domainEvent.ClientId,
                NoticeText.Of("notice.messageFromClient.title", name), preview, $"/messages?client={domainEvent.ClientId}", key, OnlyWhileUnread: true), cancellationToken);
        }
    }
}
