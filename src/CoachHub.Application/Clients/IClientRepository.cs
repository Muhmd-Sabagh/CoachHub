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
    Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
    Task AddRenewalAsync(SubscriptionRenewal renewal, CancellationToken cancellationToken);
    Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken);
}