using CoachHub.Domain.Clients;

namespace CoachHub.Domain.Tests.Clients;

public sealed class SubscriptionRenewalTests
{
    [Fact]
    public void Renewal_appends_history_extends_end_date_and_increments_count()
    {
        var start = new DateOnly(2026, 1, 15);
        var recordedAt = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var currencyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var subscription = Create(start, renewalCount: 2);

        var renewal = subscription.Renew(3, 450, currencyId, accountId, recordedAt);

        Assert.Equal(3, renewal.SequenceNumber);
        Assert.Equal(start.AddMonths(1), renewal.PreviousEndDate);
        Assert.Equal(start.AddMonths(4), renewal.NewEndDate);
        Assert.Equal(renewal.NewEndDate, subscription.EndDate);
        Assert.Equal(4, subscription.DurationMonths);
        Assert.Equal(3, subscription.RenewalCount);
        Assert.Equal(currencyId, renewal.CurrencyId);
        Assert.Equal(accountId, renewal.PaymentAccountId);
        Assert.Equal(recordedAt, renewal.RecordedAt);
        Assert.Single(subscription.Renewals);
    }

    [Fact]
    public void Renewed_subscription_baseline_cannot_be_rewritten()
    {
        var subscription = Create(new DateOnly(2026, 1, 1));
        subscription.Renew(1, 100, Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        var action = () => subscription.Update(
            Guid.NewGuid(),
            new DateOnly(2026, 2, 1),
            2,
            200,
            Guid.NewGuid(),
            null,
            1);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(121, 100)]
    [InlineData(1, 0)]
    public void Renewal_rejects_invalid_commercial_values(int durationMonths, decimal price)
    {
        var subscription = Create(new DateOnly(2026, 1, 1));

        Assert.ThrowsAny<ArgumentException>(() =>
            subscription.Renew(
                durationMonths,
                price,
                Guid.NewGuid(),
                null,
                DateTimeOffset.UtcNow));
    }

    private static Subscription Create(DateOnly startDate, int renewalCount = 0) =>
        Subscription.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            startDate,
            1,
            100,
            Guid.NewGuid(),
            null,
            renewalCount);
}
