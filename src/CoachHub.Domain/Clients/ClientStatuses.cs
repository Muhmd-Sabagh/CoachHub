using System.Text.Json.Serialization;
namespace CoachHub.Domain.Clients;

/// <summary>
/// A client's subscription status (§47, §57). <see cref="Inactive"/> means no subscription from the current coach.
/// For one subscription: Expired once its end date is reached; otherwise Suspended while the coach has paused it,
/// Pending before it starts, else Active. For a client, the best of their subscriptions in that order: Active,
/// Suspended, Pending, Expired.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))]
public enum SubscriptionStatus
{
    Inactive,
    Active,
    Expired,
    Suspended,
    Pending
}

/// <summary>A subscription's payment status, derived from its invoices (§57).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionPaymentStatus>))]
public enum SubscriptionPaymentStatus
{
    NoInvoice,
    Unpaid,
    PartiallyPaid,
    Paid,
    Refunded
}

/// <summary>The client's status shown to the coach (§39).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientStatus>))]
public enum ClientStatus
{
    OnTrack,
    NeedsReview,
    Inactive,
    SubscriptionExpired,
    CheckInOverdue
}
