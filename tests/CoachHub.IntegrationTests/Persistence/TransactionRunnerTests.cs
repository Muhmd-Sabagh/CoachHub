using CoachHub.Application.Common;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Persistence;

/// <summary>P22: saves inside one run are stored together or not at all; a nested run joins the outer one.</summary>
public sealed class TransactionRunnerTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task A_failure_after_several_saves_keeps_none_of_them()
    {
        var first = "Rollback A " + Guid.NewGuid().ToString("N")[..6];
        var second = "Rollback B " + Guid.NewGuid().ToString("N")[..6];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
            var runner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(async token =>
            {
                db.Add(Coach.Create(first, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(token);
                await runner.RunAsync(async inner =>
                {
                    db.Add(Coach.Create(second, DateTimeOffset.UtcNow));
                    await db.SaveChangesAsync(inner);
                }, token);
                throw new InvalidOperationException("Infrastructure failure after the saves.");
            }, CancellationToken.None));
        }

        using var check = factory.Services.CreateScope();
        var names = await check.ServiceProvider.GetRequiredService<CoachHubDbContext>().Set<Coach>()
            .Where(x => x.DisplayName == first || x.DisplayName == second).CountAsync();
        Assert.Equal(0, names);
    }

    [Fact]
    public async Task A_successful_run_stores_every_save()
    {
        var name = "Committed " + Guid.NewGuid().ToString("N")[..6];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
            await scope.ServiceProvider.GetRequiredService<ITransactionRunner>().RunAsync(async token =>
            {
                db.Add(Coach.Create(name, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(token);
            }, CancellationToken.None);
        }

        using var check = factory.Services.CreateScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<CoachHubDbContext>().Set<Coach>().AnyAsync(x => x.DisplayName == name));
    }
}
