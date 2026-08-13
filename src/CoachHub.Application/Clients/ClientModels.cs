using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

public sealed record ClientCreateInput(
    string Name,
    string? Phone,
    string? Email,
    DateOnly? JoinDate);

public sealed record ClientUpdateInput(
    string Name,
    string? Phone,
    string? Email,
    PlanWorkflowStatus DietStatus,
    PlanWorkflowStatus WorkoutStatus,
    bool IsActive);

public sealed record ClientResponse(
    Guid Id,
    string ClientCode,
    string FormCode,
    string Name,
    string? Phone,
    string? Email,
    DateOnly JoinDate,
    SubscriptionStatus SubscriptionStatus,
    PlanWorkflowStatus DietStatus,
    PlanWorkflowStatus WorkoutStatus,
    bool IsActive,
    int SubscriptionCount);

public sealed record ClientDetailResponse(
    ClientResponse Client,
    IReadOnlyList<SubscriptionResponse> Subscriptions);

public sealed record ClientQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public PlanWorkflowStatus? DietStatus { get; init; }
    public PlanWorkflowStatus? WorkoutStatus { get; init; }
    public bool? IsActive { get; init; }
    public DateOnly? JoinDateFrom { get; init; }
    public DateOnly? JoinDateTo { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public ClientQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim(),
        SortBy = string.IsNullOrWhiteSpace(SortBy) ? null : SortBy.Trim().ToLowerInvariant()
    };
}

public sealed record SubscriptionInput(
    Guid PackageId,
    DateOnly StartDate,
    int DurationMonths,
    decimal Price,
    Guid CurrencyId,
    Guid? PaymentAccountId,
    int RenewalCount);

public sealed record SubscriptionRenewalInput(
    int DurationMonths,
    decimal Price,
    Guid CurrencyId,
    Guid? PaymentAccountId);

public sealed record SubscriptionRenewalResponse(
    Guid Id,
    int SequenceNumber,
    DateOnly PreviousEndDate,
    DateOnly NewEndDate,
    int DurationMonths,
    decimal Price,
    Guid CurrencyId,
    Guid? PaymentAccountId,
    DateTimeOffset RecordedAt);

public sealed record SubscriptionResponse(
    Guid Id,
    Guid ClientId,
    Guid PackageId,
    DateOnly StartDate,
    DateOnly EndDate,
    int DurationMonths,
    decimal Price,
    Guid CurrencyId,
    Guid? PaymentAccountId,
    int RenewalCount,
    bool IsActive,
    IReadOnlyList<SubscriptionRenewalResponse> Renewals);