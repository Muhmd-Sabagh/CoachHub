using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Communications;

[JsonConverter(typeof(JsonStringEnumConverter<NotificationChannel>))]
public enum NotificationChannel { Email, WhatsApp }
[JsonConverter(typeof(JsonStringEnumConverter<NotificationStatus>))]
public enum NotificationStatus { Pending, Processing, Sent, Failed, Cancelled }

public sealed class Notification : Entity, IOptionalTenantOwned
{
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

    public static Notification Schedule(Guid? coachId, Guid? clientId, NotificationChannel channel, string recipient,
        string subject, string body, DateTimeOffset scheduledAt, bool isAccountMessage)
    {
        recipient = Required(recipient, 320, nameof(recipient));
        subject = Required(subject, 200, nameof(subject));
        body = Required(body, 10_000, nameof(body));
        return new() { CoachId = TenantKey.Optional(coachId), ClientId = clientId, Channel = channel, Recipient = recipient,
            Subject = subject, Body = body, ScheduledAt = scheduledAt, Status = NotificationStatus.Pending,
            IsAccountMessage = isAccountMessage };
    }

    public void Start() { if (Status is not (NotificationStatus.Pending or NotificationStatus.Failed)) throw new InvalidOperationException(); Status = NotificationStatus.Processing; AttemptCount++; }
    public void MarkSent(DateTimeOffset at) { Status = NotificationStatus.Sent; SentAt = at; LastError = null; }
    public void MarkFailed(string error) { Status = NotificationStatus.Failed; LastError = error.Length > 1000 ? error[..1000] : error; }
    public void Cancel() { if (Status == NotificationStatus.Sent) throw new InvalidOperationException(); Status = NotificationStatus.Cancelled; }
    private static string Required(string value, int max, string name) { ArgumentException.ThrowIfNullOrWhiteSpace(value, name); value = value.Trim(); if (value.Length > max) throw new ArgumentOutOfRangeException(name); return value; }
}
