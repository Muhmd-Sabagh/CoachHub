using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Media;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.Infrastructure.Auth.Persistence;
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
        if (query.Status is { } status) clients = WithStatus(clients, status, today);
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
    // Client codes are unique across all coaches, so this check ignores tenant filters.
    public Task<bool> ClientCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().IgnoreQueryFilters().AnyAsync(client => client.ClientCode == code, cancellationToken);

    /// <summary>Reached only after the caller found the client through the tenant filter.</summary>
    public Task<ClientProfile?> FindProfileAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<ClientProfile>()
            .Include(profile => profile.Equipment)
            .Include(profile => profile.Foods)
            .SingleOrDefaultAsync(profile => profile.ClientId == clientId, cancellationToken);

    public async Task AddProfileAsync(ClientProfile profile, CancellationToken cancellationToken)
    {
        dbContext.Set<ClientProfile>().Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The computed status (§39) in SQL, matching <see cref="Client.GetStatus"/>: a manual
    /// override while it lasts, otherwise Inactive, SubscriptionExpired, NeedsReview or OnTrack.
    /// </summary>
    private static IQueryable<Client> WithStatus(IQueryable<Client> clients, ClientStatus status, DateOnly today) => status switch
    {
        ClientStatus.Inactive => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : !client.IsActive),
        ClientStatus.SubscriptionExpired => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive && client.Subscriptions.Any() &&
                  !client.Subscriptions.Any(subscription => subscription.StartDate <= today && subscription.EndDate > today)),
        ClientStatus.NeedsReview => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive &&
                  (!client.Subscriptions.Any() ||
                   client.Subscriptions.Any(subscription => subscription.StartDate <= today && subscription.EndDate > today)) &&
                  (client.DietStatus == PlanWorkflowStatus.ReviewRequired || client.WorkoutStatus == PlanWorkflowStatus.ReviewRequired)),
        ClientStatus.OnTrack => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive &&
                  (!client.Subscriptions.Any() ||
                   client.Subscriptions.Any(subscription => subscription.StartDate <= today && subscription.EndDate > today)) &&
                  client.DietStatus != PlanWorkflowStatus.ReviewRequired && client.WorkoutStatus != PlanWorkflowStatus.ReviewRequired),
        // Only a manual override yields CheckInOverdue until check-in due dates exist (P12).
        _ => clients.Where(client =>
            client.StatusOverride == status && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
    };
    public async Task AddAsync(Client client, CancellationToken cancellationToken)
    {
        dbContext.Set<Client>().Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
    public async Task<IReadOnlyList<string>> DeleteAsync(Client client, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // The client's login goes with the client; its sessions and role links cascade, and its
        // uploads lose their uploader.
        var account = await dbContext.Set<User>()
            .SingleOrDefaultAsync(user => user.ClientId == client.Id, cancellationToken);
        if (account is not null)
        {
            dbContext.Set<User>().Remove(account);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Order matters, so each step saves on its own: answers (cascading with their submission)
        // reference media, and media references the client. Coaching rows and client media
        // follow the client on transfer, so they all belong to the client's current coach.
        var submissions = await dbContext.Set<FormSubmission>()
            .Where(submission => submission.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<FormSubmission>().RemoveRange(submissions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var media = await dbContext.Set<MediaAsset>()
            .Where(asset => asset.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<MediaAsset>().RemoveRange(media);
        await dbContext.SaveChangesAsync(cancellationToken);
        var storageKeys = media.Select(asset => asset.StorageKey).ToArray();

        dbContext.Set<Client>().Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return storageKeys;
    }

    public async Task<bool> HasProtectedHistoryAsync(Client client, CancellationToken cancellationToken) =>
        await dbContext.Set<Invoice>().IgnoreQueryFilters()
            .AnyAsync(invoice => invoice.ClientId == client.Id, cancellationToken) ||
        await dbContext.Set<Subscription>().IgnoreQueryFilters()
            .AnyAsync(subscription => subscription.ClientId == client.Id &&
                (subscription.CoachId != client.CoachId || subscription.Renewals.Any()), cancellationToken);

    public async Task TransferAsync(Client client, Guid toCoachId, CancellationToken cancellationToken)
    {
        var fromCoachId = client.CoachId;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        client.TransferTo(toCoachId);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Coaching records follow the client. Commercial records (subscriptions, renewals,
        // invoices, payments, refunds) stay with the coach who sold them (D7).
        var dietPlans = await dbContext.Set<DietPlan>().IgnoreQueryFilters()
            .Where(plan => plan.ClientId == client.Id && plan.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(plan => plan.CoachId, toCoachId), cancellationToken);
        var workoutPlans = await dbContext.Set<WorkoutPlan>().IgnoreQueryFilters()
            .Where(plan => plan.ClientId == client.Id && plan.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(plan => plan.CoachId, toCoachId), cancellationToken);
        var submissions = await dbContext.Set<FormSubmission>().IgnoreQueryFilters()
            .Where(submission => submission.ClientId == client.Id && submission.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(submission => submission.CoachId, toCoachId), cancellationToken);
        var deliveries = await dbContext.Set<DeliveredPlan>().IgnoreQueryFilters()
            .Where(delivery => delivery.ClientId == client.Id && delivery.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(delivery => delivery.CoachId, toCoachId), cancellationToken);
        var foodLog = await dbContext.Set<FoodLogEntry>().IgnoreQueryFilters()
            .Where(entry => entry.ClientId == client.Id && entry.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(entry => entry.CoachId, toCoachId), cancellationToken);
        await dbContext.Set<NutritionDay>().IgnoreQueryFilters()
            .Where(day => day.ClientId == client.Id && day.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(day => day.CoachId, toCoachId), cancellationToken);
        var workouts = await dbContext.Set<WorkoutSession>().IgnoreQueryFilters()
            .Where(session => session.ClientId == client.Id && session.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.CoachId, toCoachId), cancellationToken);
        await dbContext.Set<PersonalRecord>().IgnoreQueryFilters()
            .Where(record => record.ClientId == client.Id && record.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(record => record.CoachId, toCoachId), cancellationToken);
        var media = await dbContext.Set<MediaAsset>().IgnoreQueryFilters()
            .Where(asset => asset.ClientId == client.Id && asset.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.CoachId, (Guid?)toCoachId), cancellationToken);

        // The bulk moves bypass change tracking, so the transfer is recorded explicitly (§56, D2).
        dbContext.RecordAudit("ClientTransfer", client.Id, AuditOperation.Update, toCoachId, client.Id,
            new Dictionary<string, (object? From, object? To)>
            {
                ["CoachId"] = (fromCoachId, toCoachId),
                ["DietPlansMoved"] = (null, dietPlans),
                ["WorkoutPlansMoved"] = (null, workoutPlans),
                ["FormSubmissionsMoved"] = (null, submissions),
                ["DeliveredPlansMoved"] = (null, deliveries),
                ["FoodLogEntriesMoved"] = (null, foodLog),
                ["WorkoutSessionsMoved"] = (null, workouts),
                ["MediaMoved"] = (null, media)
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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