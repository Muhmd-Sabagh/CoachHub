namespace CoachHub.Application.Common;

/// <summary>
/// Runs several saves as one database transaction (P22): all of them are stored, or none. A run inside another run
/// joins the outer transaction.
/// </summary>
public interface ITransactionRunner
{
    Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
