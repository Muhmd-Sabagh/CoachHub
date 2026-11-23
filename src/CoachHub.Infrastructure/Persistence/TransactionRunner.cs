using CoachHub.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Persistence;

/// <summary>One relational transaction around the work; the in-memory test provider runs it as is.</summary>
public sealed class TransactionRunner(CoachHubDbContext dbContext) : ITransactionRunner
{
    public async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational() || dbContext.Database.CurrentTransaction is not null)
        {
            await work(cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
