using CoachHub.Application.Common.Models;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Clients;

public interface IClientRepository
{
    Task<PagedResult<Client>> ListAsync(ClientQuery query, DateOnly today, CancellationToken cancellationToken);
    Task<Client?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ClientCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> FormCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task AddAsync(Client client, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Client client, CancellationToken cancellationToken);

    /// <summary>
    /// True when deleting the client would destroy commercial records: invoices, renewals, or
    /// subscriptions sold by another coach. Checked across all coaches.
    /// </summary>
    Task<bool> HasProtectedHistoryAsync(Client client, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the client and its coaching records (assigned plans, form submissions, delivered
    /// plans) to another coach in one transaction. Commercial records stay with their coach (D7).
    /// </summary>
    Task TransferAsync(Client client, Guid toCoachId, CancellationToken cancellationToken);
    Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
    Task AddRenewalAsync(SubscriptionRenewal renewal, CancellationToken cancellationToken);
    Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
}