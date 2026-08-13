using CoachHub.Domain.Common;

namespace CoachHub.Domain.Clients;

public sealed class SubscriptionRenewal : Entity
{
    private SubscriptionRenewal() { }

    public Guid SubscriptionId { get; private set; }
    public int SequenceNumber { get; private set; }
    public DateOnly PreviousEndDate { get; private set; }
    public DateOnly NewEndDate { get; private set; }
    public int DurationMonths { get; private set; }
    public decimal Price { get; private set; }
    public Guid CurrencyId { get; private set; }
    public Guid? PaymentAccountId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    internal static SubscriptionRenewal Create(
        Guid subscriptionId,
        int sequenceNumber,
        DateOnly previousEndDate,
        int durationMonths,
        decimal price,
        Guid currencyId,
        Guid? paymentAccountId,
        DateTimeOffset recordedAt)
    {
        if (subscriptionId == Guid.Empty)
            throw new ArgumentException("A subscription is required.", nameof(subscriptionId));
        if (sequenceNumber is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        if (durationMonths is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(durationMonths));
        if (price is < 0.01m or > 1_000_000m)
            throw new ArgumentOutOfRangeException(nameof(price));
        if (currencyId == Guid.Empty)
            throw new ArgumentException("A currency is required.", nameof(currencyId));

        return new SubscriptionRenewal
        {
            SubscriptionId = subscriptionId,
            SequenceNumber = sequenceNumber,
            PreviousEndDate = previousEndDate,
            NewEndDate = previousEndDate.AddMonths(durationMonths),
            DurationMonths = durationMonths,
            Price = price,
            CurrencyId = currencyId,
            PaymentAccountId = paymentAccountId,
            RecordedAt = recordedAt
        };
    }
}
