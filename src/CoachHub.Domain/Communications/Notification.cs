using CoachHub.Domain.Common;

namespace CoachHub.Domain.Communications;

public enum NotificationChannel { Email, WhatsApp }
public enum NotificationStatus { Pending, Processing, Sent, Failed, Cancelled }

public sealed class Notification : Entity
{
    private Notification() { }
    public Guid? ClientId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; private set; }
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastError { get; private set; }

    public static Notification Schedule(Guid? clientId, NotificationChannel channel, string recipient,
        string subject, string body, DateTimeOffset scheduledAt)
    {
        recipient = Required(recipient, 320, nameof(recipient));
        subject = Required(subject, 200, nameof(subject));
        body = Required(body, 10_000, nameof(body));
        return new() { ClientId = clientId, Channel = channel, Recipient = recipient,
            Subject = subject, Body = body, ScheduledAt = scheduledAt, Status = NotificationStatus.Pending };
    }

    public void Start() { if (Status is not (NotificationStatus.Pending or NotificationStatus.Failed)) throw new InvalidOperationException(); Status = NotificationStatus.Processing; AttemptCount++; }
    public void MarkSent(DateTimeOffset at) { Status = NotificationStatus.Sent; SentAt = at; LastError = null; }
    public void MarkFailed(string error) { Status = NotificationStatus.Failed; LastError = error.Length > 1000 ? error[..1000] : error; }
    public void Cancel() { if (Status == NotificationStatus.Sent) throw new InvalidOperationException(); Status = NotificationStatus.Cancelled; }
    private static string Required(string value, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value, name); value = value.Trim(); if (value.Length > max) throw new ArgumentOutOfRangeException(name); return value; }
}
