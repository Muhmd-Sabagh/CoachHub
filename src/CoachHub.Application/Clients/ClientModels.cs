using System.Text.Json.Serialization;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

/// <param name="CoachId">Required when an administrator creates the client; ignored for coaches.</param>
/// <param name="Invite">Creates the client's login and emails a set-password invitation; needs an email.</param>
public sealed record ClientCreateInput(
    string Name,
    string? Phone,
    string? Email,
    DateOnly? JoinDate,
    Guid? CoachId = null,
    bool Invite = false);

/// <summary>A manual status (§39), until the end of <paramref name="Until"/> or until cleared.</summary>
public sealed record ClientStatusInput(ClientStatus Status, DateOnly? Until);

/// <summary>The client's login: none yet, invited (no password set), active, or disabled.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientAccountState>))]
public enum ClientAccountState
{
    None,
    Invited,
    Active,
    Disabled
}

public sealed record ClientAccountResponse(ClientAccountState State, string? Email, DateTimeOffset? LastLoginAt);

public sealed record ClientTransferInput(Guid CoachId);

public sealed record ClientUpdateInput(
    string Name,
    string? Phone,
    string? Email,
    PlanWorkflowStatus DietStatus,
    PlanWorkflowStatus WorkoutStatus,
    bool IsActive);

public sealed record ClientResponse(
    Guid Id,
    Guid CoachId,
    string ClientCode,
    string Name,
    string? Phone,
    string? Email,
    DateOnly JoinDate,
    SubscriptionStatus SubscriptionStatus,
    PlanWorkflowStatus DietStatus,
    PlanWorkflowStatus WorkoutStatus,
    bool IsActive,
    int SubscriptionCount,
    ClientStatus Status,
    ClientStatus? StatusOverride,
    DateOnly? StatusOverrideUntil);

public sealed record ClientDetailResponse(
    ClientResponse Client,
    IReadOnlyList<SubscriptionResponse> Subscriptions,
    ClientAccountResponse Account);

/// <summary>
/// The client's login account (onboarding, §2). Creating it never sets a password: the client
/// chooses one from the emailed invitation.
/// </summary>
public interface IClientAccounts
{
    Task<ClientAccountResponse> GetAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Creates the login when missing and emails a set-password invitation (again).</summary>
    Task<ClientAccountResponse> InviteAsync(Client client, CancellationToken cancellationToken);

    /// <summary>Keeps the login in step with the client: name, email, and active state (ending sessions when deactivated).</summary>
    Task SyncAsync(Client client, CancellationToken cancellationToken);
}

public sealed record ClientQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public PlanWorkflowStatus? DietStatus { get; init; }
    public PlanWorkflowStatus? WorkoutStatus { get; init; }
    public ClientStatus? Status { get; init; }
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
    IReadOnlyList<SubscriptionRenewalResponse> Renewals,
    SubscriptionStatus Status = SubscriptionStatus.Active,
    SubscriptionPaymentStatus PaymentStatus = SubscriptionPaymentStatus.NoInvoice,
    DateTimeOffset? SuspendedAt = null,
    string? SuspensionReason = null);

/// <summary>Why the coach pauses the subscription (§57 Suspended).</summary>
public sealed record SubscriptionSuspendInput(string Reason);