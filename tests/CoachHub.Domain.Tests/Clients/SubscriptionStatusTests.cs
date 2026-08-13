using CoachHub.Domain.Clients;

namespace CoachHub.Domain.Tests.Clients;

public sealed class SubscriptionStatusTests
{
    private static readonly DateOnly Today = new(2026, 8, 13);

    [Fact]
    public void Client_without_subscriptions_is_inactive()
    {
        var client = CreateClient();
        Assert.Equal(SubscriptionStatus.Inactive, client.GetSubscriptionStatus(Today));
    }

    [Fact]
    public void Active_subscription_uses_start_inclusive_end_exclusive_range()
    {
        var client = CreateClient();
        var subscription = CreateSubscription(client.Id, Today, 1);
        client.AddSubscription(subscription);

        Assert.Equal(SubscriptionStatus.Active, client.GetSubscriptionStatus(Today));
        Assert.Equal(SubscriptionStatus.Expired, client.GetSubscriptionStatus(Today.AddMonths(1)));
    }

    [Fact]
    public void Client_with_only_past_or_future_subscription_is_expired_by_business_rule()
    {
        var client = CreateClient();
        client.AddSubscription(CreateSubscription(client.Id, Today.AddMonths(1), 1));

        Assert.Equal(SubscriptionStatus.Expired, client.GetSubscriptionStatus(Today));
    }

    private static Client CreateClient() =>
        Client.Create("A1B2C3D4", "A1B2C3D4E5", "Client", null, null, Today);

    private static Subscription CreateSubscription(Guid clientId, DateOnly startDate, int months) =>
        Subscription.Create(
            clientId,
            Guid.NewGuid(),
            startDate,
            months,
            100,
            Guid.NewGuid(),
            null,
            0);
}