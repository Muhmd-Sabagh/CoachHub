using CoachHub.Application.Auth;
using CoachHub.Application.Settings;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Notifications;

/// <summary>
/// Creates event notifications (§42). Every recipient gets the in-app notification, pushed live; an email copy goes
/// through the existing outbox when the account wants that event by email and the platform's email switch is on.
/// Each recipient's copy is written in their own language, else the platform's default language (P19).
/// </summary>
public sealed class NotificationCenter(
    IInboxRepository inbox,
    IRecipientDirectory directory,
    IRealtimePublisher realtime,
    IAppLinks links,
    SettingsService settings,
    TimeProvider timeProvider)
{
    public async Task NotifyClientAsync(EventNotice notice, CancellationToken cancellationToken)
    {
        if (notice.ClientId is not { } clientId) throw new ArgumentException("A client notice names the client.", nameof(notice));
        await NotifyAsync(notice, await directory.ClientAsync(clientId, cancellationToken), cancellationToken);
    }

    public async Task NotifyCoachAsync(EventNotice notice, CancellationToken cancellationToken) =>
        await NotifyAsync(notice, await directory.CoachAsync(notice.CoachId, cancellationToken), cancellationToken);

    private async Task NotifyAsync(EventNotice notice, IReadOnlyList<NotificationRecipient> recipients, CancellationToken cancellationToken)
    {
        var targets = new List<NotificationRecipient>();
        foreach (var recipient in recipients)
        {
            if (notice.EventKey is { } key && await inbox.ExistsAsync(recipient.UserId, key, notice.OnlyWhileUnread, cancellationToken))
                continue;
            targets.Add(recipient);
        }
        if (targets.Count == 0) return;

        var now = timeProvider.GetUtcNow();
        var platform = await settings.PlatformAsync(cancellationToken);
        var emailOn = platform.EmailNotificationsEnabled;
        var preferences = await inbox.EmailPreferencesAsync(targets.Select(x => x.UserId).ToArray(), cancellationToken);
        var created = new List<Notification>();
        foreach (var recipient in targets)
        {
            var language = UiLanguages.Normalize(recipient.Language) ?? UiLanguages.Normalize(platform.DefaultLanguage) ?? UiLanguages.English;
            var title = notice.Title.Render(language);
            var body = notice.Body.Render(language);
            var inApp = Notification.ForEvent(notice.CoachId, notice.ClientId, recipient.UserId, NotificationChannel.InApp,
                recipient.UserId.ToString(), notice.Event, title, body, notice.Link, notice.EventKey, now);
            inbox.Add(inApp);
            created.Add(inApp);
            var wanted = preferences.TryGetValue(recipient.UserId, out var events) ? events : NotificationPreference.Defaults;
            if (emailOn && !string.IsNullOrWhiteSpace(recipient.Email) && NotificationPreference.WantsEmail(wanted, notice.Event))
            {
                var link = links.Absolute(notice.Link);
                inbox.Add(Notification.ForEvent(notice.CoachId, notice.ClientId, recipient.UserId, NotificationChannel.Email,
                    recipient.Email!, notice.Event, title, link is null ? body : $"{body}\n\n{link}",
                    notice.Link, notice.EventKey, now));
            }
        }
        await inbox.SaveChangesAsync(cancellationToken);
        foreach (var notification in created)
            await realtime.PublishAsync([notification.RecipientUserId!.Value], RealtimeEvents.Notification, InboxService.Map(notification), cancellationToken);
    }
}
