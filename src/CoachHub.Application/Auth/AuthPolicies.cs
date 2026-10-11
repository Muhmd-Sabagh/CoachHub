namespace CoachHub.Application.Auth;

/// <summary>Authorization policy names. Tenant isolation is enforced separately by the data layer.</summary>
public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string CoachOrAdmin = "CoachOrAdmin";
    public const string ClientOnly = "ClientOnly";
}
