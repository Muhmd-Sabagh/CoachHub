namespace CoachHub.Domain.Common;

/// <summary>
/// An entity owned by exactly one coach (tenant). Isolation is enforced centrally by the
/// persistence layer; entities only guarantee that the owner is always set.
/// </summary>
public interface ITenantOwned
{
    Guid CoachId { get; }
}

/// <summary>An entity that is either platform-level (no coach) or owned by one coach.</summary>
public interface IOptionalTenantOwned
{
    Guid? CoachId { get; }
}

/// <summary>
/// A catalog entry (foods; exercises from P9): platform entries (no coach) are visible to every
/// signed-in user, a coach's own entries to that coach and their clients. Only administrators
/// write platform entries; a coach writes only their own.
/// </summary>
public interface ICatalogEntry : IOptionalTenantOwned;

public static class TenantKey
{
    public static Guid Required(Guid coachId) =>
        coachId == Guid.Empty ? throw new ArgumentException("A coach is required.", nameof(coachId)) : coachId;

    public static Guid? Optional(Guid? coachId) =>
        coachId == Guid.Empty ? throw new ArgumentException("A coach identifier cannot be empty.", nameof(coachId)) : coachId;
}
