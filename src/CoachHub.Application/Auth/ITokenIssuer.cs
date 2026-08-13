namespace CoachHub.Application.Auth;

public interface ITokenIssuer
{
    IssuedToken Issue(AuthenticatedUser user);
}
