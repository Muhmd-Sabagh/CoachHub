using CoachHub.Domain.Communications;

namespace CoachHub.Application.Communications;

public sealed record NotificationInput(Guid? ClientId, NotificationChannel Channel, string Recipient, string Subject, string Body, DateTimeOffset? ScheduledAt);
public sealed record NotificationResponse(Guid Id, Guid? ClientId, NotificationChannel Channel, string Recipient, string Subject, DateTimeOffset ScheduledAt, NotificationStatus Status, int AttemptCount, DateTimeOffset? SentAt, string? LastError);
public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ListAsync(CancellationToken token);
    Task<IReadOnlyList<Notification>> DueAsync(DateTimeOffset now, int take, CancellationToken token);
    Task<Notification?> FindAsync(Guid id, CancellationToken token);
    Task AddAsync(Notification notification, CancellationToken token);
    Task SaveAsync(CancellationToken token);
}
public interface INotificationSender { NotificationChannel Channel { get; } Task SendAsync(string recipient, string subject, string body, CancellationToken token); }
