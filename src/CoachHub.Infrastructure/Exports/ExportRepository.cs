using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Exports;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Billing;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Progress;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Exports;

/// <summary>
/// Export reads (§51). Every query runs under the caller's tenant filters, so a coach exports their own clients and an
/// Administrator the platform. Nothing here lifts the filters.
/// </summary>
public sealed class ExportRepository(CoachHubDbContext dbContext, ICurrentUser currentUser) : IExportRepository
{
    public async Task<IReadOnlyList<ExportClient>> ClientsAsync(CancellationToken cancellationToken)
    {
        var clients = await dbContext.Set<Client>().AsNoTracking().Include(x => x.Subscriptions).ToArrayAsync(cancellationToken);
        var coaches = await CoachNamesAsync(clients.Select(x => x.CoachId), cancellationToken);
        return clients.Select(x => new ExportClient(x, coaches.GetValueOrDefault(x.CoachId, string.Empty))).ToArray();
    }

    public async Task<(Client Client, ClientProfile? Profile, string CoachName)?> ClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await dbContext.Set<Client>().AsNoTracking().Include(x => x.Subscriptions)
            .SingleOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        if (client is null) return null;
        var profile = await dbContext.Set<ClientProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
        var coach = await CoachNamesAsync([client.CoachId], cancellationToken);
        return (client, profile, coach.GetValueOrDefault(client.CoachId, string.Empty));
    }

    public async Task<ExportProgress> ProgressAsync(Guid clientId, CancellationToken cancellationToken) => new(
        await Entries<WeightEntry>(clientId, cancellationToken),
        await Entries<BodyFatEntry>(clientId, cancellationToken),
        await Entries<BodyMeasurement>(clientId, cancellationToken),
        await Entries<SleepEntry>(clientId, cancellationToken),
        await Entries<StepsEntry>(clientId, cancellationToken),
        await Entries<WaterEntry>(clientId, cancellationToken));

    public async Task<IReadOnlyList<FoodLogEntry>> FoodLogAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<FoodLogEntry>().AsNoTracking().Where(x => x.ClientId == clientId).ToArrayAsync(cancellationToken);

    public async Task<(IReadOnlyList<WorkoutSession> Sessions, IReadOnlyDictionary<Guid, string> ExerciseNames)> WorkoutsAsync(
        Guid clientId, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.Set<WorkoutSession>().AsNoTracking().Where(x => x.ClientId == clientId).ToArrayAsync(cancellationToken);
        var ids = sessions.SelectMany(x => x.Exercises).Select(x => x.ExerciseId).Distinct().ToArray();
        // The catalog filter applies: a previous coach's custom exercise shows without its name after a transfer.
        var names = await dbContext.Set<Exercise>().AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NameEn, cancellationToken);
        return (sessions, names);
    }

    public async Task<IReadOnlyList<ExportCheckIn>> CheckInsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var checkIns = await dbContext.Set<CheckIn>().AsNoTracking().Where(x => x.ClientId == clientId).ToArrayAsync(cancellationToken);
        var submissions = checkIns.Select(x => x.SubmissionId).ToArray();
        // Answers belong to the submissions of the client's own check-ins read above.
        var answers = (await dbContext.Set<FormAnswer>().AsNoTracking().Where(x => submissions.Contains(x.FormSubmissionId))
            .ToArrayAsync(cancellationToken)).ToLookup(x => x.FormSubmissionId);
        var formIds = checkIns.Select(x => x.FormDefinitionId).Distinct().ToArray();
        var forms = await dbContext.Set<FormDefinition>().AsNoTracking().Where(x => formIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return checkIns.Select(x => new ExportCheckIn(x, forms.GetValueOrDefault(x.FormDefinitionId, "Check-in"), answers[x.SubmissionId].ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<ExportCoach>> CoachesAsync(CancellationToken cancellationToken)
    {
        var counts = await dbContext.Set<Client>().AsNoTracking().GroupBy(x => x.CoachId)
            .Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return (await dbContext.Set<Coach>().AsNoTracking().ToArrayAsync(cancellationToken))
            .Select(x => new ExportCoach(x.Id, x.DisplayName, x.IsActive, x.CreatedAt, counts.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<IReadOnlyList<ExportSubscription>> SubscriptionsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from subscription in dbContext.Set<Subscription>().AsNoTracking()
            join client in dbContext.Set<Client>().AsNoTracking() on subscription.ClientId equals client.Id
            join coach in dbContext.Set<Coach>().AsNoTracking() on subscription.CoachId equals coach.Id
            join package in dbContext.Set<Package>().AsNoTracking() on subscription.PackageId equals package.Id
            join currency in dbContext.Set<Currency>().AsNoTracking() on subscription.CurrencyId equals currency.Id
            select new { subscription, ClientName = client.Name, CoachName = coach.DisplayName, PackageName = package.NameEn, currency.Code })
            .ToArrayAsync(cancellationToken);
        return rows.Select(x => new ExportSubscription(x.subscription, x.ClientName, x.CoachName, x.PackageName, x.Code)).ToArray();
    }

    public async Task<IReadOnlyDictionary<Guid, SubscriptionPaymentStatus>> SubscriptionPaymentsAsync(
        IReadOnlyCollection<Guid> subscriptionIds, CancellationToken cancellationToken)
    {
        var invoices = (await dbContext.Set<Invoice>().AsNoTracking()
            .Where(x => x.SubscriptionId != null && subscriptionIds.Contains(x.SubscriptionId.Value))
            .Select(x => new { SubscriptionId = x.SubscriptionId!.Value, x.Total, x.Paid, x.Status })
            .ToArrayAsync(cancellationToken)).ToLookup(x => x.SubscriptionId);
        return subscriptionIds.Distinct().ToDictionary(id => id, id => SubscriptionPayments.Status(invoices[id].Select(x => (x.Total, x.Paid, x.Status))));
    }

    public async Task<IReadOnlyList<LedgerEntry>> LedgerAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var payments = await (
            from payment in dbContext.Set<Payment>().AsNoTracking()
            where payment.RecordedAt >= start && payment.RecordedAt < end &&
                payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed
            join invoice in dbContext.Set<Invoice>().AsNoTracking() on payment.InvoiceId equals invoice.Id
            join client in dbContext.Set<Client>().AsNoTracking() on invoice.ClientId equals client.Id
            join coach in dbContext.Set<Coach>().AsNoTracking() on payment.CoachId equals coach.Id
            join currency in dbContext.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id
            join accountValue in dbContext.Set<PaymentAccount>().AsNoTracking() on payment.PaymentAccountId equals accountValue.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new LedgerEntry(payment.RecordedAt, false, coach.DisplayName, client.Name, invoice.Number, currency.Code, payment.Amount,
                account == null ? null : account.Name, payment.Reference))
            .ToArrayAsync(cancellationToken);
        var refunds = await (
            from refund in dbContext.Set<Refund>().AsNoTracking()
            where refund.RecordedAt >= start && refund.RecordedAt < end
            join payment in dbContext.Set<Payment>().AsNoTracking() on refund.PaymentId equals payment.Id
            join invoice in dbContext.Set<Invoice>().AsNoTracking() on payment.InvoiceId equals invoice.Id
            join client in dbContext.Set<Client>().AsNoTracking() on invoice.ClientId equals client.Id
            join coach in dbContext.Set<Coach>().AsNoTracking() on refund.CoachId equals coach.Id
            join currency in dbContext.Set<Currency>().AsNoTracking() on invoice.CurrencyId equals currency.Id
            join accountValue in dbContext.Set<PaymentAccount>().AsNoTracking() on payment.PaymentAccountId equals accountValue.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new LedgerEntry(refund.RecordedAt, true, coach.DisplayName, client.Name, invoice.Number, currency.Code, refund.Amount,
                account == null ? null : account.Name, refund.Reason))
            .ToArrayAsync(cancellationToken);
        return [.. payments, .. refunds];
    }

    public async Task RecordAsync(string dataset, Guid? clientId, Guid? coachId, int rows, CancellationToken cancellationToken)
    {
        dbContext.RecordAudit("DataExport", clientId, AuditOperation.Create, coachId ?? currentUser.CoachId, clientId,
            new Dictionary<string, (object? From, object? To)> { ["Dataset"] = (null, dataset), ["Rows"] = (null, rows) });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<T>> Entries<T>(Guid clientId, CancellationToken cancellationToken) where T : ProgressEntry =>
        await dbContext.Set<T>().AsNoTracking().Where(x => x.ClientId == clientId).OrderBy(x => x.Date).ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> CoachNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        return await dbContext.Set<Coach>().AsNoTracking().Where(x => distinct.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
    }
}
