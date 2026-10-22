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
    public void Client_with_only_a_future_subscription_is_pending()
    {
        // P16 (§57): a subscription that has not started is Pending, no longer Expired.
        var client = CreateClient();
        client.AddSubscription(CreateSubscription(client.Id, Today.AddMonths(1), 1));

        Assert.Equal(SubscriptionStatus.Pending, client.GetSubscriptionStatus(Today));
    }

    private static readonly Guid CoachId = Guid.NewGuid();

    private static Client CreateClient() =>
        Client.Create(CoachId, "Client", null, null, Today);

    private static Subscription CreateSubscription(Guid clientId, DateOnly startDate, int months) =>
        Subscription.Create(
            CoachId,
            clientId,
            Guid.NewGuid(),
            startDate,
            months,
            100,
            Guid.NewGuid(),
            null,
            0);
}