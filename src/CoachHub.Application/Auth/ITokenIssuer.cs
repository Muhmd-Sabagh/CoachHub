namespace CoachHub.Application.Auth;

public interface ITokenIssuer
{
    /// <summary>Issues a short-lived access token bound to the session it was issued for.</summary>
    IssuedToken Issue(AuthenticatedUser user, Guid sessionId);
}
