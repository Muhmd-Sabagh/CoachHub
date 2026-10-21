using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Communications;

/// <summary>How a notification reaches its recipient. <see cref="InApp"/> is the in-app list (P14), delivered live.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationChannel>))]
public enum NotificationChannel { Email, WhatsApp, InApp }
[JsonConverter(typeof(JsonStringEnumConverter<NotificationStatus>))]
public enum NotificationStatus { Pending, Processing, Sent, Failed, Cancelled }

/// <summary>The events that notify a user (§42).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationEvent>))]
public enum NotificationEvent
{
    NewWorkout,
    NewDiet,
    NewProgram,
    ProgramUpdated,
    CheckInRequired,
    CheckInReviewed,
    NewMessage,
    SubscriptionExpired
}

public sealed class Notification : Entity, IOptionalTenantOwned
{
    public const int MaximumLinkLength = 300;
    public const int MaximumEventKeyLength = 200;

    private Notification() { }
    /// <summary>The coach that scheduled the message, or null for platform messages such as password resets.</summary>
    public Guid? CoachId { get; private set; }
    public Guid? ClientId { get; private set; }

    /// <summary>
    /// Account security mail (invitations, password resets): always sent, whatever the platform's
    /// notification switches say.
    /// </summary>
    public bool IsAccountMessage { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    // May carry one-time links (password reset); never audited.
    [AuditIgnore] public string Body { get; private set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; private set; }
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>The account an event notification is for (P14); null for manual and account mail.</summary>
    public Guid? RecipientUserId { get; private set; }
    public NotificationEvent? Event { get; private set; }
    /// <summary>Where the in-app notification leads, as an app path such as <c>/app/chat</c>.</summary>
    public string? Link { get; private set; }
    /// <summary>
    /// Identifies the occurrence, so time-based events (a check-in due date, an expired subscription) notify once
    /// per recipient.
    /// </summary>
    public string? EventKey { get; private set; }
    [AuditIgnore] public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Schedule(Guid? coachId, Guid? clientId, NotificationChannel channel, string recipient,
        string subject, string body, DateTimeOffset scheduledAt, bool isAccountMessage)
    {
        if (channel == NotificationChannel.InApp) throw new ArgumentException("In-app notifications are created for an event.", nameof(channel));
        recipient = Required(recipient, 320, nameof(recipient));
        subject = Required(subject, 200, nameof(subject));
        body = Required(body, 10_000, nameof(body));
        return new() { CoachId = TenantKey.Optional(coachId), ClientId = clientId, Channel = channel, Recipient = recipient,
            Subject = subject, Body = body, ScheduledAt = scheduledAt, Status = NotificationStatus.Pending,
            IsAccountMessage = isAccountMessage };
    }

    /// <summary>An event notification for one account (§42): in-app (shown at once) or a copy through the outbox.</summary>
    public static Notification ForEvent(Guid coachId, Guid? clientId, Guid recipientUserId, NotificationChannel channel,
        string recipient, NotificationEvent @event, string title, string body, string? link, string? eventKey, DateTimeOffset now)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (!Enum.IsDefined(@event)) throw new ArgumentException("Unknown event.", nameof(@event));
        if (channel is not (NotificationChannel.InApp or NotificationChannel.Email))
            throw new ArgumentException("Event notifications are in-app or email.", nameof(channel));
        var path = Optional(link, MaximumLinkLength, nameof(link));
        if (path is not null && !path.StartsWith('/')) throw new ArgumentException("A link is an app path.", nameof(link));
        var notification = new Notification
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = clientId,
            Channel = channel,
            Recipient = Required(recipient, 320, nameof(recipient)),
            Subject = Required(title, 200, nameof(title)),
            Body = Required(body, 10_000, nameof(body)),
            ScheduledAt = now,
            Status = channel == NotificationChannel.InApp ? NotificationStatus.Sent : NotificationStatus.Pending,
            SentAt = channel == NotificationChannel.InApp ? now : null,
            RecipientUserId = recipientUserId,
            Event = @event,
            Link = path,
            EventKey = Optional(eventKey, MaximumEventKeyLength, nameof(eventKey))
        };
        return notification;
    }

    /// <summary>The recipient opened the in-app notification; reading twice keeps the first time.</summary>
    public void MarkRead(DateTimeOffset at)
    {
        if (Channel != NotificationChannel.InApp) throw new InvalidOperationException("Only in-app notifications are read.");
        ReadAt ??= at;
    }

    public void Start() { if (Status is not (NotificationStatus.Pending or NotificationStatus.Failed)) throw new InvalidOperationException(); Status = NotificationStatus.Processing; AttemptCount++; }
    public void MarkSent(DateTimeOffset at) { Status = NotificationStatus.Sent; SentAt = at; LastError = null; }
    public void MarkFailed(string error) { Status = NotificationStatus.Failed; LastError = error.Length > 1000 ? error[..1000] : error; }
    public void Cancel() { if (Status == NotificationStatus.Sent) throw new InvalidOperationException(); Status = NotificationStatus.Cancelled; }
    private static string Required(string value, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value, name); value = value.Trim(); if (value.Length > max) throw new ArgumentOutOfRangeException(name); return value; }
    private static string? Optional(string? value, int max, string name) => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, name);
}

/// <summary>
/// Which events an account also wants by email (P14). In-app notifications always arrive; without a row the
/// defaults apply: every event except new messages.
/// </summary>
public sealed class NotificationPreference : Entity
{
    private NotificationPreference() { }

    public Guid UserId { get; private set; }
    public NotificationEmailEvents EmailEvents { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationEmailEvents Defaults =>
        NotificationEmailEvents.All & ~NotificationEmailEvents.NewMessage;

    public static NotificationPreference Create(Guid userId, NotificationEmailEvents emailEvents, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("An account is required.", nameof(userId));
        var preference = new NotificationPreference { UserId = userId };
        preference.Change(emailEvents, now);
        return preference;
    }

    public void Change(NotificationEmailEvents emailEvents, DateTimeOffset now)
    {
        if ((emailEvents & ~NotificationEmailEvents.All) != 0) throw new ArgumentException("Unknown event.", nameof(emailEvents));
        EmailEvents = emailEvents;
        UpdatedAt = now;
    }

    public static bool WantsEmail(NotificationEmailEvents emailEvents, NotificationEvent @event) =>
        emailEvents.HasFlag(Flag(@event));

    public static NotificationEmailEvents Flag(NotificationEvent @event) => (NotificationEmailEvents)(1 << (int)@event);
}

/// <summary>The events an account gets by email, one bit per <see cref="NotificationEvent"/>.</summary>
[Flags]
public enum NotificationEmailEvents
{
    None = 0,
    NewWorkout = 1 << (int)NotificationEvent.NewWorkout,
    NewDiet = 1 << (int)NotificationEvent.NewDiet,
    NewProgram = 1 << (int)NotificationEvent.NewProgram,
    ProgramUpdated = 1 << (int)NotificationEvent.ProgramUpdated,
    CheckInRequired = 1 << (int)NotificationEvent.CheckInRequired,
    CheckInReviewed = 1 << (int)NotificationEvent.CheckInReviewed,
    NewMessage = 1 << (int)NotificationEvent.NewMessage,
    SubscriptionExpired = 1 << (int)NotificationEvent.SubscriptionExpired,
    All = NewWorkout | NewDiet | NewProgram | ProgramUpdated | CheckInRequired | CheckInReviewed | NewMessage | SubscriptionExpired
}
