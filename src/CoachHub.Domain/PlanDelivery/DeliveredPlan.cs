using CoachHub.Domain.Common;

namespace CoachHub.Domain.PlanDelivery;

public enum DeliveredPlanType { Diet, Workout }
public enum DeliveryChannel { Download, Email, WhatsApp }

public sealed class DeliveredPlan : Entity
{
    private DeliveredPlan() { }
    public Guid ClientId { get; private set; }
    public DeliveredPlanType PlanType { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid VersionId { get; private set; }
    public string PlanNameSnapshot { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string SnapshotJson { get; private set; } = string.Empty;
    public DeliveryChannel Channel { get; private set; }
    public Guid? NotificationId { get; private set; }
    public DateTimeOffset DeliveredAt { get; private set; }

    public static DeliveredPlan Create(Guid clientId, DeliveredPlanType type, Guid planId, Guid versionId,
        string name, string language, string snapshotJson, DeliveryChannel channel, Guid? notificationId, DateTimeOffset at)
    {
        if (clientId == Guid.Empty || planId == Guid.Empty || versionId == Guid.Empty) throw new ArgumentException("Client, plan, and version are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        language = language.Trim().ToLowerInvariant();
        if (language is not ("en" or "ar")) throw new ArgumentException("Language must be en or ar.", nameof(language));
        return new() { ClientId = clientId, PlanType = type, PlanId = planId, VersionId = versionId,
            PlanNameSnapshot = name.Trim(), Language = language, SnapshotJson = snapshotJson,
            Channel = channel, NotificationId = notificationId, DeliveredAt = at };
    }
}
