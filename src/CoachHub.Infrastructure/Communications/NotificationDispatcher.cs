using CoachHub.Application.Communications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoachHub.Infrastructure.Communications;

public sealed class NotificationDispatcher(IServiceScopeFactory scopes, ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<NotificationService>().DispatchDueAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Notification dispatch cycle failed."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
