using CoachHub.Domain.PlanDelivery;

namespace CoachHub.Application.PlanDelivery;

/// <summary>
/// Records a plan sent to a client. <paramref name="VersionNumber"/> picks the plan version (the current
/// one when null); a diet delivery also names the variant (tab) sent, a workout delivery none.
/// </summary>
public sealed record DeliveredPlanInput(
    Guid ClientId, DeliveredPlanType PlanType, Guid PlanId, int? VersionNumber, Guid? VariantId,
    string Language, DeliveryChannel Channel, string? Recipient);
public sealed record DeliveredPlanResponse(
    Guid Id, Guid ClientId, DeliveredPlanType PlanType, Guid PlanId, Guid? RevisionId, int? VersionNumber, Guid? VariantId,
    string PlanName, string Language, DeliveryChannel Channel, Guid? NotificationId, DateTimeOffset DeliveredAt);
public sealed record PlanSnapshot(Guid CoachId, string Name, Guid RevisionId, int VersionNumber, string SnapshotJson);
public interface IPlanDeliveryRepository
{
    /// <summary>The snapshot of one plan version (and diet variant), or null when it does not exist.</summary>
    Task<PlanSnapshot?> SnapshotAsync(
        DeliveredPlanType type, Guid planId, int? versionNumber, Guid? variantId, string language, CancellationToken token);
    Task<IReadOnlyList<DeliveredPlan>> ListAsync(Guid clientId, CancellationToken token);
    Task AddAsync(DeliveredPlan delivery, CancellationToken token);
}
