using CoachHub.Domain.Common;

namespace CoachHub.Domain.Clients;

public sealed class Subscription : Entity
{
    private Subscription() { }

    public Guid ClientId { get; private set; }
    public Guid PackageId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int DurationMonths { get; private set; }
    public decimal Price { get; private set; }
    public Guid CurrencyId { get; private set; }
    public Guid? PaymentAccountId { get; private set; }
    public int RenewalCount { get; private set; }

    public static Subscription Create(
        Guid clientId,
        Guid packageId,
        DateOnly startDate,
        int durationMonths,
        decimal price,
        Guid currencyId,
        Guid? paymentAccountId,
        int renewalCount)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        var subscription = new Subscription { ClientId = clientId };
        subscription.Update(
            packageId,
            startDate,
            durationMonths,
            price,
            currencyId,
            paymentAccountId,
            renewalCount);
        return subscription;
    }

    public void Update(
        Guid packageId,
        DateOnly startDate,
        int durationMonths,
        decimal price,
        Guid currencyId,
        Guid? paymentAccountId,
        int renewalCount)
    {
        if (packageId == Guid.Empty) throw new ArgumentException("A package is required.", nameof(packageId));
        if (currencyId == Guid.Empty) throw new ArgumentException("A currency is required.", nameof(currencyId));
        if (durationMonths is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(durationMonths));
        if (price is < 0.01m or > 1_000_000m) throw new ArgumentOutOfRangeException(nameof(price));
        if (renewalCount is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(renewalCount));
        PackageId = packageId;
        StartDate = startDate;
        DurationMonths = durationMonths;
        EndDate = startDate.AddMonths(durationMonths);
        Price = price;
        CurrencyId = currencyId;
        PaymentAccountId = paymentAccountId;
        RenewalCount = renewalCount;
    }

    public bool IsActiveOn(DateOnly date) => date >= StartDate && date < EndDate;
}