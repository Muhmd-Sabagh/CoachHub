namespace CoachHub.Domain.Clients;

public enum SubscriptionStatus
{
    Inactive,
    Active,
    Expired
}

public enum PlanWorkflowStatus
{
    NotStarted,
    WaitingForPlan,
    OnPlan,
    ReviewRequired
}