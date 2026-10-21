using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Chat;

/// <summary>Who wrote a chat message: the client's coach or the client (§41).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChatParticipant>))]
public enum ChatParticipant
{
    Coach,
    Client
}

/// <summary>
/// The chat between a client and their coach (§41); one open conversation per client. When the client moves to
/// another coach the conversation is archived: it stays readable for the client only, and the new coach starts a new
/// one (owner decision 2026-10-22).
/// </summary>
public sealed class Conversation : Entity, ITenantOwned
{
    public const int MaximumCoachNameLength = 200;

    private Conversation() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // Bookkeeping, also visible on every message.
    [AuditIgnore] public DateTimeOffset? LastMessageAt { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    /// <summary>The coach the archived conversation was with, shown to the client.</summary>
    public string? ArchivedCoachName { get; private set; }

    public static Conversation Start(Guid coachId, Guid clientId, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        return new Conversation { CoachId = TenantKey.Required(coachId), ClientId = clientId, CreatedAt = now };
    }

    /// <summary>Ends the conversation on transfer: read-only from now on.</summary>
    public void Archive(string coachName, DateTimeOffset now)
    {
        if (IsArchived) return;
        var name = string.IsNullOrWhiteSpace(coachName) ? "Previous coach" : coachName.Trim();
        IsArchived = true;
        ArchivedAt = now;
        ArchivedCoachName = name.Length > MaximumCoachNameLength ? name[..MaximumCoachNameLength] : name;
    }

    internal void Touch(DateTimeOffset at) => LastMessageAt = at;
}

/// <summary>A file sent with a chat message (§41): an uploaded media file of the client.</summary>
public sealed record ChatAttachmentInput(Guid MediaId, string FileName, string ContentType, long SizeBytes);

/// <summary>A chat message (§41): text, attachments, or both, and when the other side read it.</summary>
public sealed class ChatMessage : Entity, ITenantOwned
{
    public const int MaximumTextLength = 4000;
    public const int MaximumAttachments = 10;

    private readonly List<ChatAttachment> _attachments = [];

    private ChatMessage() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid ConversationId { get; private set; }
    public ChatParticipant Sender { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string? Text { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    /// <summary>When the other side read the message (read status).</summary>
    [AuditIgnore] public DateTimeOffset? ReadAt { get; private set; }
    public IReadOnlyCollection<ChatAttachment> Attachments => _attachments;

    public static ChatMessage Send(
        Conversation conversation, ChatParticipant sender, Guid senderUserId, string? text,
        IReadOnlyCollection<ChatAttachmentInput> attachments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(attachments);
        if (conversation.IsArchived) throw new InvalidOperationException("The conversation is archived.");
        if (!Enum.IsDefined(sender)) throw new ArgumentException("Unknown sender.", nameof(sender));
        if (senderUserId == Guid.Empty) throw new ArgumentException("A sender is required.", nameof(senderUserId));
        var body = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (body is { Length: > MaximumTextLength })
            throw new ArgumentException($"A message has at most {MaximumTextLength} characters.", "text");
        if (body is null && attachments.Count == 0) throw new ArgumentException("Write a message or attach a file.", "text");
        if (attachments.Count > MaximumAttachments)
            throw new ArgumentException($"At most {MaximumAttachments} files per message.", "attachments");
        if (attachments.Select(x => x.MediaId).Distinct().Count() != attachments.Count)
            throw new ArgumentException("Each file is attached once.", "attachments");

        var message = new ChatMessage
        {
            CoachId = conversation.CoachId,
            ClientId = conversation.ClientId,
            ConversationId = conversation.Id,
            Sender = sender,
            SenderUserId = senderUserId,
            Text = body,
            SentAt = now
        };
        foreach (var attachment in attachments) message._attachments.Add(ChatAttachment.Create(message.Id, attachment));
        conversation.Touch(now);
        message.Raise(new ChatMessageSent(message.Id, conversation.Id, message.CoachId, message.ClientId, sender));
        return message;
    }

    /// <summary>The other side read the message; the first time counts.</summary>
    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;
}

public sealed class ChatAttachment : Entity
{
    public const int MaximumFileNameLength = 255;

    private ChatAttachment() { }

    public Guid MessageId { get; private set; }
    public Guid MediaId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    internal static ChatAttachment Create(Guid messageId, ChatAttachmentInput input)
    {
        if (input.MediaId == Guid.Empty) throw new ArgumentException("A file is required.", "attachments");
        if (string.IsNullOrWhiteSpace(input.FileName) || string.IsNullOrWhiteSpace(input.ContentType) || input.SizeBytes <= 0)
            throw new ArgumentException("The file is incomplete.", "attachments");
        var name = input.FileName.Trim();
        return new ChatAttachment
        {
            MessageId = messageId,
            MediaId = input.MediaId,
            FileName = name.Length > MaximumFileNameLength ? name[..MaximumFileNameLength] : name,
            ContentType = input.ContentType.Trim(),
            SizeBytes = input.SizeBytes
        };
    }
}

/// <summary>A message was sent: the other side is notified and both sides receive it live.</summary>
public sealed record ChatMessageSent(Guid MessageId, Guid ConversationId, Guid CoachId, Guid ClientId, ChatParticipant Sender) : IDomainEvent;
