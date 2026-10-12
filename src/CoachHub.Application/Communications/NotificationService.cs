using CoachHub.Application.Clients;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Settings;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Communications;

public sealed class NotificationService(
    INotificationRepository repository,
    IEnumerable<INotificationSender> senders,
    IClientRepository clients,
    CoachOwnership ownership,
    ICurrentUser currentUser,
    SettingsService settings,
    BrandingService branding,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(CancellationToken token) => (await repository.ListAsync(token)).Select(Map).ToArray();
    public async Task<NotificationResponse> ScheduleAsync(NotificationInput input, CancellationToken token)
    {
        Guid? coachId;
        if (input.ClientId is { } clientId)
            coachId = (await clients.FindAsync(clientId, token) ?? throw new NotFoundException("Client", clientId)).CoachId;
        else if (currentUser.IsAdministrator && input.CoachId is null)
            coachId = null;
        else
            coachId = await ownership.ForNewRecordAsync(input.CoachId, token);
        return await AddAsync(coachId, input, token);
    }

    /// <summary>Platform messages, such as account security emails, that no coach owns.</summary>
    public Task<NotificationResponse> SchedulePlatformAsync(NotificationInput input, CancellationToken token) =>
        AddAsync(null, input, token);

    private async Task<NotificationResponse> AddAsync(Guid? coachId, NotificationInput input, CancellationToken token)
    {
        if (coachId is not null && !EnabledForCoaches(await settings.PlatformAsync(token)).Contains(input.Channel))
            throw new ConflictException($"{input.Channel} notifications are turned off in the platform settings.");
        var item = Notification.Schedule(coachId, input.ClientId, input.Channel, input.Recipient, input.Subject, input.Body, input.ScheduledAt ?? timeProvider.GetUtcNow());
        await repository.AddAsync(item, token); return Map(item);
    }
    public async Task CancelAsync(Guid id, CancellationToken token) { var item = await repository.FindAsync(id, token) ?? throw new NotFoundException("Notification", id); item.Cancel(); await repository.SaveAsync(token); }
    public async Task<int> DispatchDueAsync(CancellationToken token)
    {
        var sent = 0;
        var platform = await settings.PlatformAsync(token);
        foreach (var item in await repository.DueAsync(timeProvider.GetUtcNow(), 25, EnabledForCoaches(platform), token))
        {
            item.Start(); await repository.SaveAsync(token);
            try
            {
                var sender = senders.SingleOrDefault(x => x.Channel == item.Channel) ?? throw new InvalidOperationException($"No {item.Channel} sender is configured.");
                var message = new OutgoingMessage(item.Recipient, item.Subject, item.Body, await BrandingForAsync(item, platform, token));
                await sender.SendAsync(message, token); item.MarkSent(timeProvider.GetUtcNow()); sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { item.MarkFailed(ex.Message); }
            await repository.SaveAsync(token);
        }
        return sent;
    }
    /// <summary>
    /// The brand a message goes out under: its coach's, or for a platform message about a client
    /// (a password reset) the client's coach's; otherwise the platform's.
    /// </summary>
    private async Task<MessageBranding> BrandingForAsync(Notification item, Domain.Settings.PlatformSettings platform, CancellationToken token)
    {
        var coachId = item.CoachId;
        if (coachId is null && item.ClientId is { } clientId)
            coachId = (await clients.FindAsync(clientId, token))?.CoachId;
        var brand = await branding.EffectiveAsync(coachId, token);
        var sender = brand.CoachId is null ? platform.EmailSenderName ?? brand.BrandName : brand.BrandName;
        return new MessageBranding(brand.BrandName, sender, brand.PrimaryColor, brand.EmailHeader, brand.EmailFooter);
    }

    private static NotificationChannel[] EnabledForCoaches(Domain.Settings.PlatformSettings platform) =>
    [
        .. platform.EmailNotificationsEnabled ? [NotificationChannel.Email] : Array.Empty<NotificationChannel>(),
        .. platform.WhatsAppNotificationsEnabled ? [NotificationChannel.WhatsApp] : Array.Empty<NotificationChannel>()
    ];

    private static NotificationResponse Map(Notification x) => new(x.Id, x.ClientId, x.Channel, x.Recipient, x.Subject, x.ScheduledAt, x.Status, x.AttemptCount, x.SentAt, x.LastError);
}
