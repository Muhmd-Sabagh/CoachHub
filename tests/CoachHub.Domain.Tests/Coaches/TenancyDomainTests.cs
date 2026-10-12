using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;

namespace CoachHub.Domain.Tests.Coaches;

public sealed class TenancyDomainTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    [Fact]
    public void Coach_requires_a_display_name_and_can_be_deactivated()
    {
        var coach = Coach.Create("  Coach Amina  ", DateTimeOffset.UtcNow);
        Assert.Equal("Coach Amina", coach.DisplayName);
        Assert.True(coach.IsActive);

        coach.Update("Coach Amina", isActive: false);
        Assert.False(coach.IsActive);
        Assert.Throws<ArgumentException>(() => Coach.Create(" ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Coach_owned_records_cannot_be_created_without_a_coach()
    {
        Assert.Throws<ArgumentException>(() =>
            Client.Create(Guid.Empty, "A1B2C3D4", "Client", null, null, Today));
        Assert.Throws<ArgumentException>(() =>
            Invoice.Create(Guid.Empty, Guid.NewGuid(), null, "INV-1", Guid.NewGuid(), 10m, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Client_transfer_changes_the_coach_and_rejects_a_no_op()
    {
        var original = Guid.NewGuid();
        var target = Guid.NewGuid();
        var client = Client.Create(original, "A1B2C3D4", "Client", null, null, Today);

        client.TransferTo(target);

        Assert.Equal(target, client.CoachId);
        Assert.Throws<InvalidOperationException>(() => client.TransferTo(target));
        Assert.Throws<ArgumentException>(() => client.TransferTo(Guid.Empty));
    }

    [Fact]
    public void A_client_only_accepts_subscriptions_sold_by_its_current_coach()
    {
        var coachId = Guid.NewGuid();
        var client = Client.Create(coachId, "A1B2C3D4", "Client", null, null, Today);
        var otherCoachSubscription = Subscription.Create(
            Guid.NewGuid(), client.Id, Guid.NewGuid(), Today, 1, 100m, Guid.NewGuid(), null, 0);

        Assert.Throws<ArgumentException>(() => client.AddSubscription(otherCoachSubscription));
    }

    [Fact]
    public void Renewals_payments_and_refunds_inherit_the_selling_coach()
    {
        var coachId = Guid.NewGuid();
        var subscription = Subscription.Create(
            coachId, Guid.NewGuid(), Guid.NewGuid(), Today, 1, 100m, Guid.NewGuid(), null, 0);
        var renewal = subscription.Renew(1, 100m, Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var payment = Payment.Create(coachId, Guid.NewGuid(), null, 50m, null, DateTimeOffset.UtcNow);
        var refund = payment.RecordRefund(10m, "Correction", DateTimeOffset.UtcNow);

        Assert.Equal(coachId, renewal.CoachId);
        Assert.Equal(coachId, refund.CoachId);
    }
}
