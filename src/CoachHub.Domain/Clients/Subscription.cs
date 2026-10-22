using CoachHub.Domain.Common;

namespace CoachHub.Domain.Clients;

public sealed class Subscription : Entity, ITenantOwned
{
    private readonly List<SubscriptionRenewal> _renewals = [];

    private Subscription() { }

    /// <summary>The coach who sold the subscription. Never changes, even if the client is transferred.</summary>
    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid PackageId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int DurationMonths { get; private set; }
    public decimal Price { get; private set; }
    public Guid CurrencyId { get; private set; }
    public Guid? PaymentAccountId { get; private set; }
    public int RenewalCount { get; private set; }
    /// <summary>When the coach paused the subscription (§57 Suspended); null when it runs.</summary>
    public DateTimeOffset? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public IReadOnlyCollection<SubscriptionRenewal> Renewals => _renewals;

    public const int MaximumReasonLength = 500;

    public static Subscription Create(
        Guid coachId,
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
        var subscription = new Subscription { CoachId = TenantKey.Required(coachId), ClientId = clientId };
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
        if (_renewals.Count > 0)
        {
            throw new InvalidOperationException("A renewed subscription cannot be edited.");
        }

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

    public SubscriptionRenewal Renew(
        int durationMonths,
        decimal price,
        Guid currencyId,
        Guid? paymentAccountId,
        DateTimeOffset recordedAt)
    {
        if (RenewalCount >= 1000)
        {
            throw new InvalidOperationException("The subscription renewal limit has been reached.");
        }

        var renewal = SubscriptionRenewal.Create(
            CoachId,
            Id,
            RenewalCount + 1,
            EndDate,
            durationMonths,
            price,
            currencyId,
            paymentAccountId,
            recordedAt);
        EndDate = renewal.NewEndDate;
        DurationMonths += durationMonths;
        RenewalCount++;
        _renewals.Add(renewal);
        return renewal;
    }

    /// <summary>Running on the day: started, not ended and not suspended.</summary>
    public bool IsActiveOn(DateOnly date) => StatusOn(date) == SubscriptionStatus.Active;

    /// <summary>The subscription's own status on a day; see <see cref="SubscriptionStatus"/>.</summary>
    public SubscriptionStatus StatusOn(DateOnly date) =>
        date >= EndDate ? SubscriptionStatus.Expired
        : SuspendedAt is not null ? SubscriptionStatus.Suspended
        : date < StartDate ? SubscriptionStatus.Pending
        : SubscriptionStatus.Active;

    /// <summary>Pauses the subscription by hand (owner decision 2026-10-22: the end date does not move).</summary>
    public void Suspend(string reason, DateOnly today, DateTimeOffset now)
    {
        if (StatusOn(today) == SubscriptionStatus.Expired) throw new InvalidOperationException("An ended subscription cannot be suspended.");
        if (SuspendedAt is not null) throw new InvalidOperationException("The subscription is already suspended.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Give a reason.", nameof(reason));
        var text = reason.Trim();
        if (text.Length > MaximumReasonLength) throw new ArgumentException($"A reason has at most {MaximumReasonLength} characters.", nameof(reason));
        SuspendedAt = now;
        SuspensionReason = text;
    }

    public void Resume()
    {
        if (SuspendedAt is null) throw new InvalidOperationException("The subscription is not suspended.");
        SuspendedAt = null;
        SuspensionReason = null;
    }
}
