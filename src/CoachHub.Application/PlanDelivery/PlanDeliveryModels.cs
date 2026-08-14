using CoachHub.Domain.PlanDelivery;

namespace CoachHub.Application.PlanDelivery;

public sealed record DeliveredPlanInput(Guid ClientId, DeliveredPlanType PlanType, Guid PlanId, Guid VersionId, string Language, DeliveryChannel Channel, string? Recipient);
public sealed record DeliveredPlanResponse(Guid Id, Guid ClientId, DeliveredPlanType PlanType, Guid PlanId, Guid VersionId, string PlanName, string Language, DeliveryChannel Channel, Guid? NotificationId, DateTimeOffset DeliveredAt);
public interface IPlanDeliveryRepository
{
    Task<(string Name, string SnapshotJson)?> SnapshotAsync(DeliveredPlanType type, Guid planId, Guid versionId, string language, CancellationToken token);
    Task<IReadOnlyList<DeliveredPlan>> ListAsync(Guid clientId, CancellationToken token);
    Task AddAsync(DeliveredPlan delivery, CancellationToken token);
}
