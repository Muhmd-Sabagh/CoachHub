using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Common;

namespace CoachHub.Application.Common.Models;

/// <summary>One version of a diet or workout plan (D4, §59).</summary>
public sealed record PlanRevisionInfo(
    Guid Id, int VersionNumber, PlanRevisionStatus Status, DateOnly? StartDate, DateOnly? EndDate,
    DateTimeOffset CreatedAt, Guid? CreatedByUserId, string? CreatedByName,
    DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt, bool IsCurrent, bool IsReadOnly);

public static class PlanRevisions
{
    public static PlanRevisionInfo Info(PlanRevision revision, IReadOnlyDictionary<Guid, string> authorNames) => new(
        revision.Id, revision.VersionNumber, revision.Status, revision.StartDate, revision.EndDate,
        revision.CreatedAt, revision.CreatedByUserId,
        revision.CreatedByUserId is { } author && authorNames.TryGetValue(author, out var name) ? name : null,
        revision.UpdatedAt, revision.ArchivedAt, !revision.IsArchived, revision.IsArchived);

    /// <summary>
    /// Optimistic check for the editor: a save based on a version that is no longer current would
    /// silently overwrite or fork someone else's newer version.
    /// </summary>
    public static void EnsureBase(PlanRevision current, int? baseVersionNumber)
    {
        if (baseVersionNumber is { } basis && basis != current.VersionNumber)
            throw new ConflictException(
                $"Version {basis} is no longer current; version {current.VersionNumber} is. Reload the plan before saving.");
    }

    /// <summary>
    /// The version in force on <paramref name="day"/> among a client's plans (diet P8, workout P10). Each plan
    /// contributes the version that was current that day (the oldest one not archived by then); it applies when it is
    /// not a draft and its dates cover the day. With several plans, the latest start wins, then the newest plan.
    /// </summary>
    public static T? InForce<T>(
        IEnumerable<T> versions, DateOnly day, Func<T, Guid> planId, Func<T, PlanRevision> revision, Func<T, DateTimeOffset> planCreatedAt)
        where T : class =>
        versions.GroupBy(planId)
            .Select(plan => plan.Where(x => revision(x).ArchivedAt is not { } archived ||
                    DateOnly.FromDateTime(archived.UtcDateTime) > day)
                .MinBy(x => revision(x).VersionNumber))
            .OfType<T>()
            .Where(x => revision(x).Status != PlanRevisionStatus.Draft && revision(x).Covers(day))
            .OrderByDescending(x => revision(x).StartDate ?? DateOnly.MinValue)
            .ThenByDescending(planCreatedAt)
            .FirstOrDefault();
}
