using CoachHub.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Persistence;

/// <summary>Helpers shared by the diet and workout plan repositories (D4).</summary>
internal static class PlanAuthors
{
    public static async Task<IReadOnlyDictionary<Guid, string>> FindNamesAsync(
        CoachHubDbContext dbContext, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();
        return await dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
    }

    /// <summary>
    /// Saves a version change. A concurrent "save as new version" of the same plan loses the race on the
    /// unique version-number and current-version indexes; report it as a conflict, not a server error.
    /// </summary>
    public static async Task SaveVersionAsync(CoachHubDbContext dbContext, CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            throw new ConflictException("Another version of this plan was saved at the same time. Reload the plan and try again.");
        }
    }
}
