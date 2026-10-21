using CoachHub.Application.Common.Models;
using CoachHub.Domain.Chat;

namespace CoachHub.Application.Chat;

public sealed record ChatSendInput(string? Text, IReadOnlyList<Guid>? MediaIds = null);

public sealed record ChatAttachmentResponse(Guid MediaId, string FileName, string ContentType, long SizeBytes);

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    ChatParticipant Sender,
    string? Text,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt,
    IReadOnlyList<ChatAttachmentResponse> Attachments);

/// <summary>Messages newest first; <see cref="HasMore"/> when older ones exist (page with <c>before</c>).</summary>
public sealed record ChatMessagesPage(IReadOnlyList<ChatMessageResponse> Items, bool HasMore);

/// <summary>The open conversation with the client: null until the first message.</summary>
public sealed record ChatThreadResponse(
    Guid ClientId,
    string ClientName,
    Guid? ConversationId,
    int UnreadCount,
    ChatMessagesPage Messages);

/// <summary>A conversation with a previous coach, read-only (owner decision 2026-10-22).</summary>
public sealed record ArchivedConversationResponse(Guid Id, string CoachName, DateTimeOffset ArchivedAt, DateTimeOffset? LastMessageAt);

public sealed record MyChatResponse(ChatThreadResponse Current, IReadOnlyList<ArchivedConversationResponse> Archived);

/// <summary>The coach's conversations, most recent first.</summary>
public sealed record ConversationSummary(
    Guid ConversationId,
    Guid ClientId,
    string ClientName,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    ChatParticipant? LastSender,
    int UnreadCount);

public sealed record ConversationQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool UnreadOnly { get; init; }
    public string? SearchTerm { get; init; }

    public ConversationQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim()
    };
}

/// <summary>The read status the other side receives live.</summary>
public sealed record ChatReadResponse(Guid ConversationId, ChatParticipant Reader, DateTimeOffset ReadAt);

public sealed record ChatClient(Guid Id, Guid CoachId, string Name);

public interface IChatRepository
{
    Task<ChatClient?> FindClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<Conversation?> FindOpenAsync(Guid clientId, CancellationToken cancellationToken);
    Task<Conversation?> FindAsync(Guid clientId, Guid conversationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Conversation>> ListArchivedAsync(Guid clientId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ChatMessage> Items, bool HasMore)> MessagesAsync(
        Guid conversationId, DateTimeOffset? before, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatMessage>> UnreadAsync(Guid conversationId, ChatParticipant reader, CancellationToken cancellationToken);
    Task<int> UnreadCountAsync(Guid conversationId, ChatParticipant reader, CancellationToken cancellationToken);
    Task<PagedResult<ConversationSummary>> ListForCoachAsync(ConversationQuery query, CancellationToken cancellationToken);
    Task<ChatMessage?> FindMessageAsync(Guid id, CancellationToken cancellationToken);
    void Add(Conversation conversation);
    void Add(ChatMessage message);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
