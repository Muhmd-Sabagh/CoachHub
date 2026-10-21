using CoachHub.Application.Common.Models;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

public interface IClientRepository
{
    /// <summary>The client's profile with its equipment and food choices; null until first saved.</summary>
    Task<ClientProfile?> FindProfileAsync(Guid clientId, CancellationToken cancellationToken);
    /// <summary>The client's most recent weigh-in in kg (P11), or null without one.</summary>
    Task<decimal?> FindLatestWeightKgAsync(Guid clientId, CancellationToken cancellationToken);
    Task AddProfileAsync(ClientProfile profile, CancellationToken cancellationToken);
    Task<PagedResult<Client>> ListAsync(ClientQuery query, DateOnly today, CancellationToken cancellationToken);
    Task<Client?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ClientCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task AddAsync(Client client, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the client with their login account, sessions and media rows in one transaction.
    /// Returns the storage keys of the deleted media so their content can be removed afterwards.
    /// </summary>
    Task<IReadOnlyList<string>> DeleteAsync(Client client, CancellationToken cancellationToken);

    /// <summary>
    /// True when deleting the client would destroy commercial records: invoices, renewals, or
    /// subscriptions sold by another coach. Checked across all coaches.
    /// </summary>
    Task<bool> HasProtectedHistoryAsync(Client client, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the client and its coaching records (assigned plans, form submissions, delivered
    /// plans, the client's media) to another coach in one transaction. Commercial records stay with their coach (D7).
    /// </summary>
    Task TransferAsync(Client client, Guid toCoachId, CancellationToken cancellationToken);
    Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
    Task AddRenewalAsync(SubscriptionRenewal renewal, CancellationToken cancellationToken);
    Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
}