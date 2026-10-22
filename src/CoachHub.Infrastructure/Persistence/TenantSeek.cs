using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Common;

namespace CoachHub.Infrastructure.Persistence;

/// <summary>
/// Query-plan help for coach-wide lists (P18). The tenant filters (<see cref="TenantModel"/>) stay authoritative, but
/// their shape, <c>@unrestricted = 1 OR CoachId = @coach …</c>, is one cached plan for every caller, so SQL Server
/// scans instead of seeking the coach's index. Repeating the coach's own key as a plain predicate lets it seek: a
/// coach's audit trail page over 600,000 rows went from 15,387 logical reads to 88. It only ever narrows a query
/// (it adds a condition the tenant filter already implies), so it cannot widen what a caller sees.
/// </summary>
internal static class TenantSeek
{
    /// <summary>For a coach, adds <c>CoachId == the coach</c>; other callers are left to the tenant filters.</summary>
    public static IQueryable<T> SeekCoach<T>(this IQueryable<T> query, CoachHubDbContext dbContext) where T : class, ITenantOwned
    {
        if (!dbContext.TenantCoachScoped) return query;
        var coachId = dbContext.TenantCoachId;
        return query.Where(x => x.CoachId == coachId);
    }

    /// <inheritdoc cref="SeekCoach{T}(IQueryable{T}, CoachHubDbContext)"/>
    public static IQueryable<T> SeekOptionalCoach<T>(this IQueryable<T> query, CoachHubDbContext dbContext) where T : class, IOptionalTenantOwned
    {
        if (!dbContext.TenantCoachScoped) return query;
        Guid? coachId = dbContext.TenantCoachId;
        return query.Where(x => x.CoachId == coachId);
    }
}
