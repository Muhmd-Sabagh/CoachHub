namespace CoachHub.Application.Auth;

public interface IIdentityGateway
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
