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

public static class TenantKey
{
    public static Guid Required(Guid coachId) =>
        coachId == Guid.Empty ? throw new ArgumentException("A coach is required.", nameof(coachId)) : coachId;

    public static Guid? Optional(Guid? coachId) =>
        coachId == Guid.Empty ? throw new ArgumentException("A coach identifier cannot be empty.", nameof(coachId)) : coachId;
}
