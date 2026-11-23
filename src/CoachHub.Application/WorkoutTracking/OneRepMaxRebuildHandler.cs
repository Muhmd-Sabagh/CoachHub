using CoachHub.Application.Common.Events;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>
/// A coach's new e1RM formula (P22) rebuilds the stored PRs of their clients, so records and analytics agree at once.
/// </summary>
public sealed class OneRepMaxRebuildHandler(WorkoutTrackingService training) : IDomainEventHandler<OneRepMaxFormulaChanged>
{
    public Task HandleAsync(OneRepMaxFormulaChanged domainEvent, CancellationToken cancellationToken) =>
        training.RebuildCoachRecordsAsync(domainEvent.CoachId, cancellationToken);
}
