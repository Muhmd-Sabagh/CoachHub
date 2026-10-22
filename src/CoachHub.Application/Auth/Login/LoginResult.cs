namespace CoachHub.Application.Auth.Login;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? CoachId,
    Guid? ClientId,
    // The account's saved interface language, applied by the app at sign-in (P19); null when none is saved.
    string? PreferredLanguage = null);
