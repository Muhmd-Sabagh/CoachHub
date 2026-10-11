namespace CoachHub.Application.Auth;

public interface IIdentityGateway
{
    /// <summary>Authenticates by email address or user name.</summary>
    Task<AuthenticatedUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reloads a user for a session refresh: null when the account was deactivated, locked out or
    /// deleted, or its coach was deactivated. Roles and the client's current coach are re-read, so
    /// a refreshed token reflects role changes and client transfers.
    /// </summary>
    Task<AuthenticatedUser?> FindActiveAsync(Guid userId, CancellationToken cancellationToken);
}
