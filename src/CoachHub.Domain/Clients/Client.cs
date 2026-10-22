using System.Net.Mail;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Clients;

public sealed class Client : Entity, ITenantOwned
{
    private readonly List<Subscription> _subscriptions = [];

    private Client() { }

    public Guid CoachId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DateOnly JoinDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>A status the coach set by hand (§39), in force until <see cref="StatusOverrideUntil"/>.</summary>
    public ClientStatus? StatusOverride { get; private set; }

    /// <summary>The last day the override applies; null keeps it until the coach clears it.</summary>
    public DateOnly? StatusOverrideUntil { get; private set; }

    /// <summary>The next check-in due date (P12); null without an active schedule. Maintained by the check-in workflow.</summary>
    public DateOnly? CheckInDueOn { get; private set; }

    /// <summary>Whether a submitted check-in waits for the coach's review (P12). Maintained by the check-in workflow.</summary>
    public bool CheckInAwaitingReview { get; private set; }
    public IReadOnlyCollection<Subscription> Subscriptions => _subscriptions;

    public static Client Create(
        Guid coachId,
        string name,
        string? phone,
        string? email,
        DateOnly joinDate)
    {
        var client = new Client
        {
            CoachId = TenantKey.Required(coachId),
            JoinDate = joinDate,
            IsActive = true
        };
        client.Update(name, phone, email, true);
        return client;
    }

    public void Update(
        string name,
        string? phone,
        string? email,
        bool isActive)
    {
        Name = Required(name, 255, nameof(name));
        Phone = Optional(phone, 50, nameof(phone));
        Email = NormalizeEmail(email);
        IsActive = isActive;
    }

    /// <summary>
    /// The client's status (§39). A coach's override wins while it lasts; otherwise, in order:
    /// Inactive, SubscriptionExpired, NeedsReview (a submitted check-in awaits the coach), CheckInOverdue (the
    /// check-in due date has passed, P12), OnTrack. <c>ClientRepository.WithStatus</c> computes the same in SQL.
    /// </summary>
    public ClientStatus GetStatus(DateOnly today)
    {
        if (StatusOverride is { } manual && (StatusOverrideUntil is null || StatusOverrideUntil >= today)) return manual;
        if (!IsActive) return ClientStatus.Inactive;
        if (GetSubscriptionStatus(today) == SubscriptionStatus.Expired) return ClientStatus.SubscriptionExpired;
        if (CheckInAwaitingReview) return ClientStatus.NeedsReview;
        if (CheckInDueOn is { } due && due < today) return ClientStatus.CheckInOverdue;
        return ClientStatus.OnTrack;
    }

    /// <summary>Sets the status by hand (§39), optionally until a date at most a year away.</summary>
    public void OverrideStatus(ClientStatus status, DateOnly? until, DateOnly today)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentException("Unknown status.", nameof(status));
        if (until is { } last && (last < today || last > today.AddYears(1)))
            throw new ArgumentException("The override must end between today and one year from now.", nameof(until));
        StatusOverride = status;
        StatusOverrideUntil = until;
    }

    /// <summary>The check-in facts the status (§39) reads: the next due date and whether a check-in awaits review.</summary>
    public void SetCheckInState(DateOnly? dueOn, bool awaitingReview)
    {
        CheckInDueOn = dueOn;
        CheckInAwaitingReview = awaitingReview;
    }

    public void ClearStatusOverride()
    {
        StatusOverride = null;
        StatusOverrideUntil = null;
    }

    /// <summary>
    /// Moves the client to another coach. Commercial history stays with the coach who sold it (D7).
    /// </summary>
    public void TransferTo(Guid coachId)
    {
        coachId = TenantKey.Required(coachId);
        if (coachId == CoachId)
        {
            throw new InvalidOperationException("The client already belongs to this coach.");
        }
        CoachId = coachId;
    }

    public void AddSubscription(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (subscription.ClientId != Id)
        {
            throw new ArgumentException("Subscription belongs to another client.", nameof(subscription));
        }
        if (subscription.CoachId != CoachId)
        {
            throw new ArgumentException("Subscription was sold by another coach.", nameof(subscription));
        }
        _subscriptions.Add(subscription);
    }
    /// <summary>
    /// The best of the client's subscriptions on a day: Active, then Suspended, Pending, Expired; Inactive without
    /// any. <c>ClientRepository.WithSubscriptionStatus</c> computes the same in SQL.
    /// </summary>
    public SubscriptionStatus GetSubscriptionStatus(DateOnly today)
    {
        var statuses = _subscriptions.Select(subscription => subscription.StatusOn(today)).ToHashSet();
        foreach (var status in new[] { SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatus.Pending, SubscriptionStatus.Expired })
            if (statuses.Contains(status)) return status;
        return SubscriptionStatus.Inactive;
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = Optional(value, 255, nameof(value));
        if (normalized is null) return null;
        if (!MailAddress.TryCreate(normalized, out var address) ||
            !address.Address.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A valid email address is required.", nameof(value));
        }
        return normalized.ToLowerInvariant();
    }

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}