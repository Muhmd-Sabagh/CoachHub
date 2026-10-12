using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.PlanDelivery;

[JsonConverter(typeof(JsonStringEnumConverter<DeliveredPlanType>))]
public enum DeliveredPlanType { Diet, Workout }
[JsonConverter(typeof(JsonStringEnumConverter<DeliveryChannel>))]
public enum DeliveryChannel { Download, Email, WhatsApp }

/// <summary>
/// An immutable record of a plan sent to a client (append-only). It names the plan version that was
/// delivered (D4) and keeps its own snapshot, so later versions never change what the client received.
/// </summary>
public sealed class DeliveredPlan : Entity, ITenantOwned
{
    private DeliveredPlan() { }
    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DeliveredPlanType PlanType { get; private set; }
    public Guid PlanId { get; private set; }
    /// <summary>The delivered version. Null only for deliveries recorded before plans were versioned.</summary>
    public Guid? RevisionId { get; private set; }
    public int? RevisionNumber { get; private set; }
    /// <summary>The delivered diet variant (tab); workouts have none.</summary>
    public Guid? VariantId { get; private set; }
    public string PlanNameSnapshot { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string SnapshotJson { get; private set; } = string.Empty;
    public DeliveryChannel Channel { get; private set; }
    public Guid? NotificationId { get; private set; }
    public DateTimeOffset DeliveredAt { get; private set; }

    public static DeliveredPlan Create(Guid coachId, Guid clientId, DeliveredPlanType type, Guid planId,
        Guid revisionId, int revisionNumber, Guid? variantId,
        string name, string language, string snapshotJson, DeliveryChannel channel, Guid? notificationId, DateTimeOffset at)
    {
        if (clientId == Guid.Empty || planId == Guid.Empty || revisionId == Guid.Empty)
            throw new ArgumentException("Client, plan, and plan version are required.");
        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        if (type == DeliveredPlanType.Diet && (variantId is null || variantId == Guid.Empty))
            throw new ArgumentException("A diet delivery names the delivered variant.", nameof(variantId));
        if (type == DeliveredPlanType.Workout && variantId is not null)
            throw new ArgumentException("Workout deliveries have no variant.", nameof(variantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentException.ThrowIfNullOrWhiteSpace(snapshotJson);
        language = language.Trim().ToLowerInvariant();
        if (language is not ("en" or "ar")) throw new ArgumentException("Language must be en or ar.", nameof(language));
        return new() { CoachId = TenantKey.Required(coachId), ClientId = clientId, PlanType = type, PlanId = planId,
            RevisionId = revisionId, RevisionNumber = revisionNumber, VariantId = variantId,
            PlanNameSnapshot = name.Trim(), Language = language, SnapshotJson = snapshotJson,
            Channel = channel, NotificationId = notificationId, DeliveredAt = at };
    }
}
