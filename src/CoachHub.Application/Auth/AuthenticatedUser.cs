namespace CoachHub.Application.Auth;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? CoachId,
    Guid? ClientId,
    // The account's interface language ("en" or "ar"), or null to keep the app's current one (P19).
    string? PreferredLanguage = null);
