using System.Text.Json.Serialization;
namespace CoachHub.Domain.Clients;

[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))]
public enum SubscriptionStatus
{
    Inactive,
    Active,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter<PlanWorkflowStatus>))]
public enum PlanWorkflowStatus
{
    NotStarted,
    WaitingForPlan,
    OnPlan,
    ReviewRequired
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
