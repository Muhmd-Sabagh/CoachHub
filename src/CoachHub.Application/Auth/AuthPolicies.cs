namespace CoachHub.Application.Auth;

/// <summary>Authorization policy names. Tenant isolation is enforced separately by the data layer.</summary>
public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string CoachOrAdmin = "CoachOrAdmin";
    public const string ClientOnly = "ClientOnly";

    /// <summary>Any signed-in account (self-service such as /me); data access stays tenant-filtered.</summary>
    public const string AnyRole = "AnyRole";
}
