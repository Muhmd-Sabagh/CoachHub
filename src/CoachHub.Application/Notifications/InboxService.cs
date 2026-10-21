using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Settings;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Notifications;

/// <summary>
/// The signed-in account's in-app notifications (§42) and which events it also wants by email. Always the account in
/// the token, never one named in the request.
/// </summary>
public sealed class InboxService(IInboxRepository inbox, ICurrentUser currentUser, SettingsService settings, TimeProvider timeProvider)
{
    public async Task<PagedResult<InboxItem>> ListAsync(InboxQuery query, CancellationToken cancellationToken)
    {
        var page = await inbox.ListAsync(UserId(), query.Normalize(), cancellationToken);
        return new PagedResult<InboxItem>(page.Items.Select(Map).ToArray(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<UnreadCountResponse> UnreadCountAsync(CancellationToken cancellationToken) =>
        new(await inbox.UnreadCountAsync(UserId(), cancellationToken));

    public async Task<InboxItem> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await inbox.FindAsync(UserId(), id, cancellationToken) ?? throw new NotFoundException("Notification", id);
        notification.MarkRead(timeProvider.GetUtcNow());
        await inbox.SaveChangesAsync(cancellationToken);
        return Map(notification);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var notification in await inbox.UnreadAsync(UserId(), cancellationToken)) notification.MarkRead(now);
        await inbox.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationPreferencesResponse> PreferencesAsync(CancellationToken cancellationToken)
    {
        var preference = await inbox.FindPreferenceAsync(UserId(), cancellationToken);
        return await MapAsync(preference?.EmailEvents ?? NotificationPreference.Defaults, cancellationToken);
    }

    public async Task<NotificationPreferencesResponse> SetPreferencesAsync(NotificationPreferencesInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var events = NotificationEmailEvents.None;
        foreach (var item in input.EmailEvents ?? [])
        {
            if (!Enum.IsDefined(item))
                throw new ValidationException(new Dictionary<string, string[]> { ["emailEvents"] = ["Unknown event."] });
            events |= NotificationPreference.Flag(item);
        }
        var userId = UserId();
        var now = timeProvider.GetUtcNow();
        var preference = await inbox.FindPreferenceAsync(userId, cancellationToken);
        if (preference is null) inbox.Add(NotificationPreference.Create(userId, events, now));
        else preference.Change(events, now);
        await inbox.SaveChangesAsync(cancellationToken);
        return await MapAsync(events, cancellationToken);
    }

    internal static InboxItem Map(Notification x) =>
        new(x.Id, x.Event!.Value, x.Subject, x.Body, x.Link, x.ClientId, x.ScheduledAt, x.ReadAt);

    private async Task<NotificationPreferencesResponse> MapAsync(NotificationEmailEvents events, CancellationToken cancellationToken) =>
        new(Enum.GetValues<NotificationEvent>().Where(x => NotificationPreference.WantsEmail(events, x)).ToArray(),
            (await settings.PlatformAsync(cancellationToken)).EmailNotificationsEnabled);

    private Guid UserId() => currentUser.UserId ?? throw new ForbiddenException("Sign in to see notifications.");
}
