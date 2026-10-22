using CoachHub.Application.Common.Localization;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Notifications;

/// <summary>An account to notify, with its email address for the email copy.</summary>
/// <param name="Language">The account's saved interface language; null uses the platform default (P19).</param>
public sealed record NotificationRecipient(Guid UserId, string? Email, string? Language = null);

/// <summary>The active accounts behind a client or a coach. Accounts are not tenant data, so this is a port.</summary>
public interface IRecipientDirectory
{
    Task<IReadOnlyList<NotificationRecipient>> ClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationRecipient>> CoachAsync(Guid coachId, CancellationToken cancellationToken);
    Task<string?> ClientNameAsync(Guid clientId, CancellationToken cancellationToken);
}

/// <summary>Turns an app path into an absolute link for emails; null when the app address is not configured.</summary>
public interface IAppLinks
{
    string? Absolute(string path);
}

/// <summary>Pushes an event to the signed-in sessions of accounts (SignalR in the API). Best effort.</summary>
public interface IRealtimePublisher
{
    Task PublishAsync(IReadOnlyCollection<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken);
}

/// <summary>The live events the web app listens to.</summary>
public static class RealtimeEvents
{
    public const string Notification = "notification";
    public const string ChatMessage = "chatMessage";
    public const string ChatRead = "chatRead";
}

public interface IInboxRepository
{
    Task<PagedResult<Notification>> ListAsync(Guid userId, InboxQuery query, CancellationToken cancellationToken);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken);
    Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> UnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Whether the account already has the in-app notification for the occurrence (only unread ones when asked).</summary>
    Task<bool> ExistsAsync(Guid userId, string eventKey, bool unreadOnly, CancellationToken cancellationToken);

    Task<NotificationPreference?> FindPreferenceAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, NotificationEmailEvents>> EmailPreferencesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    void Add(Notification notification);
    void Add(NotificationPreference preference);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>A plan as the notifications describe it.</summary>
public sealed record PlanSummary(Guid CoachId, Guid? ClientId, string Name, PlanRevisionStatus CurrentStatus);

public interface IPlanLookup
{
    Task<PlanSummary?> FindAsync(PlanKind kind, Guid planId, CancellationToken cancellationToken);
}

/// <summary>A client whose next check-in is open (§42 "check-in required").</summary>
public sealed record DueCheckIn(Guid ClientId, Guid CoachId, DateOnly DueOn);

/// <summary>A client whose subscriptions have all ended (§42 "subscription expired").</summary>
public sealed record ExpiredSubscription(Guid ClientId, Guid CoachId, string ClientName, DateOnly EndedOn);

/// <summary>The time-based events, read across tenants by the background scan.</summary>
public interface INotificationEventSource
{
    Task<IReadOnlyList<DueCheckIn>> DueCheckInsAsync(DateOnly dueOnOrBefore, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExpiredSubscription>> ExpiredSubscriptionsAsync(DateOnly today, DateOnly endedSince, CancellationToken cancellationToken);
}

/// <summary>
/// One event to notify about. <see cref="EventKey"/> makes it happen once per account; with
/// <see cref="OnlyWhileUnread"/> a new one is created again once the previous one was read.
/// </summary>
public sealed record EventNotice(
    NotificationEvent Event,
    Guid CoachId,
    Guid? ClientId,
    NoticeText Title,
    NoticeText Body,
    string Link,
    string? EventKey,
    bool OnlyWhileUnread = false);

public sealed record InboxQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool UnreadOnly { get; init; }

    public InboxQuery Normalize() => this with { PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 50) };
}

public sealed record InboxItem(
    Guid Id,
    NotificationEvent Event,
    string Title,
    string Body,
    string? Link,
    Guid? ClientId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UnreadCountResponse(int Count);

/// <summary>The events the account also gets by email; in-app notifications always arrive.</summary>
public sealed record NotificationPreferencesResponse(IReadOnlyList<NotificationEvent> EmailEvents, bool EmailEnabledOnPlatform);

public sealed record NotificationPreferencesInput(IReadOnlyList<NotificationEvent> EmailEvents);

/// <summary>
/// A notification's title or body (P19): a message-catalog entry and its values, written in each recipient's own
/// language when the notification is created. A value may itself be a <see cref="NoticeText"/>. Verbatim text (a chat
/// message preview) is shown as written.
/// </summary>
public sealed class NoticeText
{
    private readonly string? code;
    private readonly object?[] values;
    private readonly string? verbatim;

    private NoticeText(string? code, object?[] values, string? verbatim)
    {
        this.code = code;
        this.values = values;
        this.verbatim = verbatim;
    }

    public static NoticeText Of(string code, params object?[] values) => new(code, values, null);

    public static NoticeText Verbatim(string text) => new(null, [], text);

    public string Render(string language) =>
        verbatim ?? Messages.Text(code!, language, values.Select(value => value is NoticeText text ? text.Render(language) : value).ToArray());
}
