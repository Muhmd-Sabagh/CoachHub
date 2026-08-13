namespace CoachHub.Application.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
