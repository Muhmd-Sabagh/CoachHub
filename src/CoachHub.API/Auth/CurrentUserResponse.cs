namespace CoachHub.API.Auth;

public sealed record CurrentUserResponse(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    Guid? CoachId,
    Guid? ClientId);
