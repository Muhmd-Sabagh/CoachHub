namespace CoachHub.Application.Common.Tenancy;

/// <summary>How much coach-owned (tenant) data the current caller may see.</summary>
public enum TenantScope
{
    /// <summary>No tenant restriction: platform administrators and background/system work.</summary>
    Unrestricted,

    /// <summary>A coach sees only their own tenant.</summary>
    Coach,

    /// <summary>A client sees only their own records inside their coach's tenant.</summary>
    Client,

    /// <summary>An anonymous caller sees no tenant data.</summary>
    None
}

/// <summary>The caller on whose behalf the current unit of work runs.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? DisplayName { get; }
    string? Role { get; }
    TenantScope Scope { get; }
    Guid? CoachId { get; }
    Guid? ClientId { get; }
    bool IsAdministrator { get; }
}
