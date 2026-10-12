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
        if (input.Language?.Trim().ToLowerInvariant() is not ("en" or "ar"))
            throw Validation("language", "Language must be en or ar.");
        if (input.PlanType == DeliveredPlanType.Diet && input.VariantId is null)
            throw Validation("variantId", "Choose the diet variant to deliver.");
        if (input.PlanType == DeliveredPlanType.Workout && input.VariantId is not null)
            throw Validation("variantId", "Workout plans have no variants.");
        var client = await clients.FindAsync(input.ClientId, token) ?? throw new NotFoundException("Client", input.ClientId);
        var snapshot = await repository.SnapshotAsync(input.PlanType, input.PlanId, input.VersionNumber, input.VariantId, input.Language, token)
            ?? throw new NotFoundException("Plan version", input.VersionNumber is { } number ? $"{input.PlanId} v{number}" : input.PlanId);
        CoachOwnership.EnsureSameCoach(client.CoachId, snapshot.CoachId, "planId", "The plan belongs to another coach.");
        Guid? notificationId = null;
        if (input.Channel is DeliveryChannel.Email or DeliveryChannel.WhatsApp)
        {
            if (string.IsNullOrWhiteSpace(input.Recipient)) throw Validation("recipient", "A recipient is required.");
            var channel = input.Channel == DeliveryChannel.Email ? NotificationChannel.Email : NotificationChannel.WhatsApp;
            var notification = await notifications.ScheduleAsync(new(input.ClientId, channel, input.Recipient, $"Your {input.PlanType} plan", $"Your {snapshot.Name} plan is ready.", null), token);
            notificationId = notification.Id;
        }
        DeliveredPlan delivery;
        try
        {
            delivery = DeliveredPlan.Create(client.CoachId, input.ClientId, input.PlanType, input.PlanId,
                snapshot.RevisionId, snapshot.VersionNumber, input.VariantId,
                snapshot.Name, input.Language, snapshot.SnapshotJson, input.Channel, notificationId, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception) { throw Validation("delivery", exception.Message); }
        await repository.AddAsync(delivery, token); return Map(delivery);
    }
    private static DeliveredPlanResponse Map(DeliveredPlan x) => new(x.Id, x.ClientId, x.PlanType, x.PlanId, x.RevisionId, x.RevisionNumber, x.VariantId,
        x.PlanNameSnapshot, x.Language, x.Channel, x.NotificationId, x.DeliveredAt);
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
}
