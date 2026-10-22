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
using CoachHub.Domain.Calendar;
using CoachHub.Domain.Chat;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.ClientFiles;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.Communications;
using CoachHub.Domain.FitnessTests;
using CoachHub.Domain.Progress;
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
                (client.Phone != null && client.Phone.Contains(query.SearchTerm)) ||
                (client.Email != null && client.Email.Contains(query.SearchTerm)));
        }
        if (query.IsActive.HasValue)
            clients = clients.Where(client => client.IsActive == query.IsActive.Value);
        if (query.JoinDateFrom.HasValue)
            clients = clients.Where(client => client.JoinDate >= query.JoinDateFrom.Value);
        if (query.JoinDateTo.HasValue)
            clients = clients.Where(client => client.JoinDate <= query.JoinDateTo.Value);
        if (query.Status is { } status) clients = WithStatus(clients, status, today);
        if (query.SubscriptionStatus is { } subscriptionStatus)
            clients = WithSubscriptionStatus(clients, subscriptionStatus, today);

        IOrderedQueryable<Client> sorted = query.SortBy switch
        {
            "name" => query.SortDescending
                ? clients.OrderByDescending(client => client.Name)
                : clients.OrderBy(client => client.Name),
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

    /// <summary>Reached only after the caller found the client through the tenant filter.</summary>
    public Task<decimal?> FindLatestWeightKgAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<WeightEntry>().Where(entry => entry.ClientId == clientId).OrderByDescending(entry => entry.Date)
            .Select(entry => (decimal?)entry.WeightKg).FirstOrDefaultAsync(cancellationToken);

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
    /// The computed status (§39) in SQL, matching <see cref="Client.GetStatus"/>: a manual override while it lasts,
    /// otherwise Inactive, SubscriptionExpired, NeedsReview (a plan or a check-in awaits review), CheckInOverdue (P12)
    /// or OnTrack.
    /// </summary>
    /// <summary>
    /// <see cref="Client.GetSubscriptionStatus"/> in SQL (P16): Active (started, not ended, not suspended), else
    /// Suspended (not ended, paused), else Pending (not started), else Expired; Inactive without subscriptions.
    /// </summary>
    internal static IQueryable<Client> WithSubscriptionStatus(IQueryable<Client> clients, SubscriptionStatus status, DateOnly today) => status switch
    {
        SubscriptionStatus.Active => clients.Where(client => client.Subscriptions.Any(s =>
            s.StartDate <= today && s.EndDate > today && s.SuspendedAt == null)),
        SubscriptionStatus.Suspended => clients.Where(client =>
            !client.Subscriptions.Any(s => s.StartDate <= today && s.EndDate > today && s.SuspendedAt == null) &&
            client.Subscriptions.Any(s => s.EndDate > today && s.SuspendedAt != null)),
        SubscriptionStatus.Pending => clients.Where(client =>
            !client.Subscriptions.Any(s => s.EndDate > today && (s.SuspendedAt != null || s.StartDate <= today)) &&
            client.Subscriptions.Any(s => s.StartDate > today)),
        SubscriptionStatus.Expired => clients.Where(client =>
            client.Subscriptions.Any() && !client.Subscriptions.Any(s => s.EndDate > today)),
        _ => clients.Where(client => !client.Subscriptions.Any())
    };

    public async Task<IReadOnlyDictionary<Guid, SubscriptionPaymentStatus>> SubscriptionPaymentsAsync(
        IReadOnlyCollection<Guid> subscriptionIds, CancellationToken cancellationToken)
    {
        if (subscriptionIds.Count == 0) return new Dictionary<Guid, SubscriptionPaymentStatus>();
        var invoices = await dbContext.Set<Invoice>().AsNoTracking()
            .Where(invoice => invoice.SubscriptionId != null && subscriptionIds.Contains(invoice.SubscriptionId.Value))
            .Select(invoice => new { SubscriptionId = invoice.SubscriptionId!.Value, invoice.Total, invoice.Paid, invoice.Status })
            .ToArrayAsync(cancellationToken);
        var bySubscription = invoices.ToLookup(x => x.SubscriptionId);
        return subscriptionIds.Distinct().ToDictionary(id => id,
            id => SubscriptionPayments.Status(bySubscription[id].Select(x => (x.Total, x.Paid, x.Status))));
    }

    internal static IQueryable<Client> WithStatus(IQueryable<Client> clients, ClientStatus status, DateOnly today) => status switch
    {
        ClientStatus.Inactive => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : !client.IsActive),
        ClientStatus.SubscriptionExpired => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive && client.Subscriptions.Any() &&
                  !client.Subscriptions.Any(subscription => subscription.EndDate > today)),
        ClientStatus.NeedsReview => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive &&
                  (!client.Subscriptions.Any() ||
                   client.Subscriptions.Any(subscription => subscription.EndDate > today)) &&
                  client.CheckInAwaitingReview),
        ClientStatus.CheckInOverdue => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive &&
                  (!client.Subscriptions.Any() ||
                   client.Subscriptions.Any(subscription => subscription.EndDate > today)) &&
                  !client.CheckInAwaitingReview && client.CheckInDueOn != null && client.CheckInDueOn < today),
        ClientStatus.OnTrack => clients.Where(client =>
            (client.StatusOverride != null && (client.StatusOverrideUntil == null || client.StatusOverrideUntil >= today))
                ? client.StatusOverride == status
                : client.IsActive &&
                  (!client.Subscriptions.Any() ||
                   client.Subscriptions.Any(subscription => subscription.EndDate > today)) &&
                  !client.CheckInAwaitingReview && (client.CheckInDueOn == null || client.CheckInDueOn >= today)),
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

        // Order matters, so each step saves on its own: check-ins reference submissions, answers
        // (cascading with their submission) reference media, and media references the client. Coaching
        // rows and client media follow the client on transfer, so they all belong to the client's current coach.
        var checkIns = await dbContext.Set<CheckIn>()
            .Where(checkIn => checkIn.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<CheckIn>().RemoveRange(checkIns);
        await dbContext.SaveChangesAsync(cancellationToken);

        var submissions = await dbContext.Set<FormSubmission>()
            .Where(submission => submission.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<FormSubmission>().RemoveRange(submissions);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Chat attachments and shared files reference the client's media too (P14).
        var conversations = await dbContext.Set<Conversation>()
            .Where(conversation => conversation.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<Conversation>().RemoveRange(conversations);
        var files = await dbContext.Set<ClientFile>()
            .Where(file => file.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<ClientFile>().RemoveRange(files);
        await dbContext.SaveChangesAsync(cancellationToken);
        // Notifications about the client go too, including platform mail (a password reset) that no coach owns and
        // the coach could not otherwise remove: a deliberately cross-tenant, standalone query of the client's rows.
        var notifications = dbContext.Set<Notification>().IgnoreQueryFilters()
            .Where(notification => notification.ClientId == client.Id);
        if (dbContext.Database.IsRelational())
        {
            var deleted = await notifications.ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                // The bulk delete bypasses change tracking, so it is recorded explicitly (D2).
                dbContext.RecordAudit("ClientNotifications", client.Id, AuditOperation.Delete, client.CoachId, client.Id,
                    new Dictionary<string, (object? From, object? To)> { ["NotificationsDeleted"] = (deleted, null) });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            dbContext.Set<Notification>().RemoveRange(await notifications.ToArrayAsync(cancellationToken));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Progress photos reference the client's media too (P11).
        var photos = await dbContext.Set<ProgressPhoto>()
            .Where(photo => photo.ClientId == client.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<ProgressPhoto>().RemoveRange(photos);
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
        // The check-in schedule names the previous coach's form, so it ends; the new coach sets their own (P12).
        var schedule = await dbContext.Set<CheckInSchedule>().SingleOrDefaultAsync(x => x.ClientId == client.Id, cancellationToken);
        if (schedule is not null) dbContext.Set<CheckInSchedule>().Remove(schedule);
        client.SetCheckInState(null, client.CheckInAwaitingReview);
        // The open chat is archived: readable by the client only; the new coach starts a new one (P14, owner decision).
        var openChat = await dbContext.Set<Conversation>().IgnoreQueryFilters()
            .SingleOrDefaultAsync(conversation => conversation.ClientId == client.Id && !conversation.IsArchived, cancellationToken);
        if (openChat is not null)
        {
            var coachName = await dbContext.Set<Coach>().Where(coach => coach.Id == fromCoachId)
                .Select(coach => coach.DisplayName).SingleOrDefaultAsync(cancellationToken);
            openChat.Archive(coachName ?? string.Empty, DateTimeOffset.UtcNow);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
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
        var progress = 0;
        progress += await MoveAsync<WeightEntry>(client.Id, fromCoachId, toCoachId, cancellationToken);
        progress += await MoveAsync<BodyFatEntry>(client.Id, fromCoachId, toCoachId, cancellationToken);
        progress += await MoveAsync<BodyMeasurement>(client.Id, fromCoachId, toCoachId, cancellationToken);
        progress += await MoveAsync<SleepEntry>(client.Id, fromCoachId, toCoachId, cancellationToken);
        progress += await MoveAsync<StepsEntry>(client.Id, fromCoachId, toCoachId, cancellationToken);
        progress += await MoveAsync<WaterEntry>(client.Id, fromCoachId, toCoachId, cancellationToken);
        var photos = await dbContext.Set<ProgressPhoto>().IgnoreQueryFilters()
            .Where(photo => photo.ClientId == client.Id && photo.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(photo => photo.CoachId, toCoachId), cancellationToken);
        // Targets are coaching data too; the new coach's defaults apply where the client has none.
        await dbContext.Set<LifestyleTargets>().IgnoreQueryFilters()
            .Where(targets => targets.ClientId == client.Id && targets.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(targets => targets.CoachId, toCoachId), cancellationToken);
        var checkInsMoved = await dbContext.Set<CheckIn>().IgnoreQueryFilters()
            .Where(checkIn => checkIn.ClientId == client.Id && checkIn.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(checkIn => checkIn.CoachId, toCoachId), cancellationToken);
        // Test results follow the client and keep their test, even a previous coach's custom one (P13). Assignments
        // of such tests end, because the new coach cannot see them; platform-test assignments move.
        var testResults = await dbContext.Set<TestResult>().IgnoreQueryFilters()
            .Where(result => result.ClientId == client.Id && result.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(result => result.CoachId, toCoachId), cancellationToken);
        var testAssignmentsEnded = await dbContext.Set<TestAssignment>().IgnoreQueryFilters()
            .Where(assignment => assignment.ClientId == client.Id && assignment.CoachId == fromCoachId &&
                dbContext.Set<TestDefinition>().Any(test => test.Id == assignment.TestDefinitionId && test.CoachId != null))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.Set<TestAssignment>().IgnoreQueryFilters()
            .Where(assignment => assignment.ClientId == client.Id && assignment.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(assignment => assignment.CoachId, toCoachId), cancellationToken);
        // Chats (archived above, so hidden from the new coach) and shared files move with the client, as do the
        // in-app notifications of the client's own account.
        var conversationsMoved = await dbContext.Set<Conversation>().IgnoreQueryFilters()
            .Where(conversation => conversation.ClientId == client.Id && conversation.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(conversation => conversation.CoachId, toCoachId), cancellationToken);
        await dbContext.Set<ChatMessage>().IgnoreQueryFilters()
            .Where(message => message.ClientId == client.Id && message.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.CoachId, toCoachId), cancellationToken);
        var filesMoved = await dbContext.Set<ClientFile>().IgnoreQueryFilters()
            .Where(file => file.ClientId == client.Id && file.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(file => file.CoachId, toCoachId), cancellationToken);
        // Calendar (P15): training days and the events shared with the client move; the previous coach's private
        // events and follow-ups about the client stay in their calendar, no longer linked to the client.
        await dbContext.Set<TrainingDays>().IgnoreQueryFilters()
            .Where(days => days.ClientId == client.Id && days.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(days => days.CoachId, toCoachId), cancellationToken);
        var eventsMoved = await dbContext.Set<CalendarEvent>().IgnoreQueryFilters()
            .Where(item => item.ClientId == client.Id && item.CoachId == fromCoachId && item.VisibleToClient)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.CoachId, toCoachId), cancellationToken);
        await dbContext.Set<CalendarEvent>().IgnoreQueryFilters()
            .Where(item => item.ClientId == client.Id && item.CoachId == fromCoachId && !item.VisibleToClient)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ClientId, (Guid?)null), cancellationToken);
        var clientAccounts = dbContext.Set<User>().Where(user => user.ClientId == client.Id).Select(user => (Guid?)user.Id);
        await dbContext.Set<Notification>().IgnoreQueryFilters()
            .Where(notification => notification.ClientId == client.Id && notification.CoachId == fromCoachId &&
                notification.Channel == NotificationChannel.InApp && clientAccounts.Contains(notification.RecipientUserId))
            .ExecuteUpdateAsync(update => update.SetProperty(notification => notification.CoachId, (Guid?)toCoachId), cancellationToken);
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
                ["ProgressEntriesMoved"] = (null, progress),
                ["ProgressPhotosMoved"] = (null, photos),
                ["CheckInsMoved"] = (null, checkInsMoved),
                ["TestResultsMoved"] = (null, testResults),
                ["TestAssignmentsEnded"] = (null, testAssignmentsEnded),
                ["ConversationsArchived"] = (null, conversationsMoved),
                ["ClientFilesMoved"] = (null, filesMoved),
                ["CalendarEventsMoved"] = (null, eventsMoved),
                ["MediaMoved"] = (null, media)
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    // Deliberately cross-tenant: the transfer moves the client's rows from the old coach to the new one.
    private Task<int> MoveAsync<T>(Guid clientId, Guid fromCoachId, Guid toCoachId, CancellationToken cancellationToken)
        where T : ProgressEntry =>
        dbContext.Set<T>().IgnoreQueryFilters()
            .Where(entry => entry.ClientId == clientId && entry.CoachId == fromCoachId)
            .ExecuteUpdateAsync(update => update.SetProperty(entry => entry.CoachId, toCoachId), cancellationToken);

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