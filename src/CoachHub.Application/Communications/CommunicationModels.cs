using CoachHub.Domain.Communications;

namespace CoachHub.Application.Communications;

/// <param name="CoachId">For messages without a client: required for coaches' own workspace when an administrator schedules on a coach's behalf; omit for platform messages.</param>
public sealed record NotificationInput(Guid? ClientId, NotificationChannel Channel, string Recipient, string Subject, string Body, DateTimeOffset? ScheduledAt, Guid? CoachId = null);
public sealed record NotificationResponse(Guid Id, Guid? ClientId, NotificationChannel Channel, string Recipient, string Subject, DateTimeOffset ScheduledAt, NotificationStatus Status, int AttemptCount, DateTimeOffset? SentAt, string? LastError);
public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ListAsync(CancellationToken token);
    /// <summary>
    /// Due notifications. Coach workspace messages are only returned on <paramref name="coachChannels"/>,
    /// so a disabled channel waits instead of failing; platform and account messages always go.
    /// </summary>
    Task<IReadOnlyList<Notification>> DueAsync(
        DateTimeOffset now, int take, IReadOnlyCollection<NotificationChannel> coachChannels, CancellationToken token);
    Task<Notification?> FindAsync(Guid id, CancellationToken token);
    Task AddAsync(Notification notification, CancellationToken token);
    Task SaveAsync(CancellationToken token);
}
public interface INotificationSender { NotificationChannel Channel { get; } Task SendAsync(OutgoingMessage message, CancellationToken token); }

/// <summary>A message ready to send, with the brand it goes out under (D1, §63).</summary>
public sealed record OutgoingMessage(string Recipient, string Subject, string Body, MessageBranding Branding);

/// <summary>The sender's brand: display name, accent color, and the email header and footer text.</summary>
public sealed record MessageBranding(string BrandName, string SenderName, string PrimaryColor, string? EmailHeader, string? EmailFooter);
