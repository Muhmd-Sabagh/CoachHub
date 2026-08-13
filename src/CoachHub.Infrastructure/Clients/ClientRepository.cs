using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Clients;

public sealed class ClientRepository(CoachHubDbContext dbContext) : IClientRepository
{
    public async Task<PagedResult<Client>> ListAsync(
        ClientQuery query,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        IQueryable<Client> clients = dbContext.Set<Client>()
            .AsNoTracking()
            .Include(client => client.Subscriptions);
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            clients = clients.Where(client =>
                client.Name.Contains(query.SearchTerm) ||
                client.ClientCode.Contains(query.SearchTerm) ||
                client.FormCode.Contains(query.SearchTerm) ||
                (client.Phone != null && client.Phone.Contains(query.SearchTerm)) ||
                (client.Email != null && client.Email.Contains(query.SearchTerm)));
        }
        if (query.IsActive.HasValue)
            clients = clients.Where(client => client.IsActive == query.IsActive.Value);
        if (query.DietStatus.HasValue)
            clients = clients.Where(client => client.DietStatus == query.DietStatus.Value);
        if (query.WorkoutStatus.HasValue)
            clients = clients.Where(client => client.WorkoutStatus == query.WorkoutStatus.Value);
        if (query.JoinDateFrom.HasValue)
            clients = clients.Where(client => client.JoinDate >= query.JoinDateFrom.Value);
        if (query.JoinDateTo.HasValue)
            clients = clients.Where(client => client.JoinDate <= query.JoinDateTo.Value);
        if (query.SubscriptionStatus.HasValue)
        {
            clients = query.SubscriptionStatus.Value switch
            {
                SubscriptionStatus.Active => clients.Where(client => client.Subscriptions.Any(
                    subscription => subscription.StartDate <= today && subscription.EndDate > today)),
                SubscriptionStatus.Expired => clients.Where(client =>
                    client.Subscriptions.Any() && !client.Subscriptions.Any(
                        subscription => subscription.StartDate <= today && subscription.EndDate > today)),
                _ => clients.Where(client => !client.Subscriptions.Any())
            };
        }

        IOrderedQueryable<Client> sorted = query.SortBy switch
        {
            "name" => query.SortDescending
                ? clients.OrderByDescending(client => client.Name)
                : clients.OrderBy(client => client.Name),
            "code" => query.SortDescending
                ? clients.OrderByDescending(client => client.ClientCode)
                : clients.OrderBy(client => client.ClientCode),
            _ => query.SortDescending
                ? clients.OrderBy(client => client.JoinDate)
                : clients.OrderByDescending(client => client.JoinDate)
        };
        var ordered = sorted.ThenBy(client => client.Id);
        var total = await ordered.LongCountAsync(cancellationToken);
        var page = await ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<Client>(page, query.PageNumber, query.PageSize, total);
    }

    public Task<Client?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Client>()
            .Include(client => client.Subscriptions)
                .ThenInclude(subscription => subscription.Renewals)
            .SingleOrDefaultAsync(client => client.Id == id, cancellationToken);
    public Task<bool> ClientCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().AnyAsync(client => client.ClientCode == code, cancellationToken);
    public Task<bool> FormCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().AnyAsync(client => client.FormCode == code, cancellationToken);
    public async Task AddAsync(Client client, CancellationToken cancellationToken)
    {
        dbContext.Set<Client>().Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
    public async Task DeleteAsync(Client client, CancellationToken cancellationToken)
    {
        dbContext.Set<Client>().Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task<Subscription?> FindSubscriptionAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Subscription>()
            .Include(subscription => subscription.Renewals)
            .SingleOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);
    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        dbContext.Set<Subscription>().Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task AddRenewalAsync(
        SubscriptionRenewal renewal,
        CancellationToken cancellationToken)
    {
        dbContext.Set<SubscriptionRenewal>().Add(renewal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task DeleteSubscriptionAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        dbContext.Set<Subscription>().Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}