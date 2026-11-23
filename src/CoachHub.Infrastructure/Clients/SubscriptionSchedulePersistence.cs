using CoachHub.Application.Clients;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoachHub.Infrastructure.Clients;

/// <summary>The scheduled subscription pass (P22). The worker runs as system work, so the tenant filters do not narrow it.</summary>
public sealed class SubscriptionScheduleRepository(CoachHubDbContext dbContext) : ISubscriptionScheduleRepository
{
    public Task<int?> OverdueSuspensionDaysAsync(CancellationToken cancellationToken) =>
        dbContext.Set<PlatformSettings>().Select(x => x.OverdueSuspensionDays).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListResumeDueAsync(DateOnly today, CancellationToken cancellationToken) =>
        await dbContext.Set<Subscription>()
            .Where(x => x.SuspendedAt != null && x.ResumesOn != null && x.ResumesOn <= today)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListWithOverdueInvoicesAsync(DateTimeOffset dueBy, CancellationToken cancellationToken)
    {
        var overdue = dbContext.Set<Invoice>()
            .Where(invoice => invoice.SubscriptionId != null && invoice.DueAt != null && invoice.DueAt <= dueBy &&
                (invoice.Status == InvoiceStatus.Issued || invoice.Status == InvoiceStatus.PartiallyPaid))
            .Select(invoice => invoice.SubscriptionId!.Value);
        return await dbContext.Set<Subscription>().Where(x => overdue.Contains(x.Id)).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> ListSuspendedForOverdueAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<Subscription>().Where(x => x.SuspendedForOverdueInvoice).ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>Runs the scheduled subscription pass every hour (P22). Version 1 runs one API instance, so one worker.</summary>
public sealed class SubscriptionScheduleWorker(IServiceScopeFactory scopes, ILogger<SubscriptionScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SubscriptionScheduleService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Scheduled subscription pass failed."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
