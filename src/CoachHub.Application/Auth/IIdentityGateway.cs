namespace CoachHub.Application.Auth;

public interface IIdentityGateway
{
    /// <summary>Authenticates by email address or user name.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken);
}
