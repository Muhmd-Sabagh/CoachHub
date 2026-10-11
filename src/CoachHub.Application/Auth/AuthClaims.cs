namespace CoachHub.Application.Auth;

public static class AuthClaims
{
    public const string CoachId = "coach_id";
    public const string ClientId = "client_id";

    /// <summary>The <see cref="Domain.Auth.UserSession"/> the access token was issued for.</summary>
    public const string SessionId = "sid";
}
