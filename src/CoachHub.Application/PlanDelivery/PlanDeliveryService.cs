using CoachHub.Application.Clients;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Communications;
using CoachHub.Domain.Communications;
using CoachHub.Domain.PlanDelivery;

namespace CoachHub.Application.PlanDelivery;

public sealed class PlanDeliveryService(IPlanDeliveryRepository repository, IClientRepository clients, NotificationService notifications, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<DeliveredPlanResponse>> ListAsync(Guid clientId, CancellationToken token) => (await repository.ListAsync(clientId, token)).Select(Map).ToArray();
    public async Task<DeliveredPlanResponse> DeliverAsync(DeliveredPlanInput input, CancellationToken token)
    {
        var client = await clients.FindAsync(input.ClientId, token) ?? throw new NotFoundException("Client", input.ClientId);
        var snapshot = await repository.SnapshotAsync(input.PlanType, input.PlanId, input.VersionId, input.Language, token) ?? throw new NotFoundException("Plan version", input.VersionId);
        CoachOwnership.EnsureSameCoach(client.CoachId, snapshot.CoachId, "planId", "The plan belongs to another coach.");
        Guid? notificationId = null;
        if (input.Channel is DeliveryChannel.Email or DeliveryChannel.WhatsApp)
        {
            if (string.IsNullOrWhiteSpace(input.Recipient)) throw new ValidationException(new Dictionary<string, string[]> { ["recipient"] = ["A recipient is required."] });
            var channel = input.Channel == DeliveryChannel.Email ? NotificationChannel.Email : NotificationChannel.WhatsApp;
            var notification = await notifications.ScheduleAsync(new(input.ClientId, channel, input.Recipient, $"CoachHub {input.PlanType} plan", $"Your {snapshot.Name} plan is ready in CoachHub.", null), token);
            notificationId = notification.Id;
        }
        var delivery = DeliveredPlan.Create(client.CoachId, input.ClientId, input.PlanType, input.PlanId, input.VersionId, snapshot.Name, input.Language, snapshot.SnapshotJson, input.Channel, notificationId, timeProvider.GetUtcNow());
        await repository.AddAsync(delivery, token); return Map(delivery);
    }
    private static DeliveredPlanResponse Map(DeliveredPlan x) => new(x.Id, x.ClientId, x.PlanType, x.PlanId, x.VersionId, x.PlanNameSnapshot, x.Language, x.Channel, x.NotificationId, x.DeliveredAt);
}
