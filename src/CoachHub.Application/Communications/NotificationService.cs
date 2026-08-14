using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Communications;

namespace CoachHub.Application.Communications;

public sealed class NotificationService(INotificationRepository repository, IEnumerable<INotificationSender> senders, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(CancellationToken token) => (await repository.ListAsync(token)).Select(Map).ToArray();
    public async Task<NotificationResponse> ScheduleAsync(NotificationInput input, CancellationToken token)
    {
        var item = Notification.Schedule(input.ClientId, input.Channel, input.Recipient, input.Subject, input.Body, input.ScheduledAt ?? timeProvider.GetUtcNow());
        await repository.AddAsync(item, token); return Map(item);
    }
    public async Task CancelAsync(Guid id, CancellationToken token) { var item = await repository.FindAsync(id, token) ?? throw new NotFoundException("Notification", id); item.Cancel(); await repository.SaveAsync(token); }
    public async Task<int> DispatchDueAsync(CancellationToken token)
    {
        var sent = 0;
        foreach (var item in await repository.DueAsync(timeProvider.GetUtcNow(), 25, token))
        {
            item.Start(); await repository.SaveAsync(token);
            try { var sender = senders.SingleOrDefault(x => x.Channel == item.Channel) ?? throw new InvalidOperationException($"No {item.Channel} sender is configured."); await sender.SendAsync(item.Recipient, item.Subject, item.Body, token); item.MarkSent(timeProvider.GetUtcNow()); sent++; }
            catch (Exception ex) when (ex is not OperationCanceledException) { item.MarkFailed(ex.Message); }
            await repository.SaveAsync(token);
        }
        return sent;
    }
    private static NotificationResponse Map(Notification x) => new(x.Id, x.ClientId, x.Channel, x.Recipient, x.Subject, x.ScheduledAt, x.Status, x.AttemptCount, x.SentAt, x.LastError);
}
