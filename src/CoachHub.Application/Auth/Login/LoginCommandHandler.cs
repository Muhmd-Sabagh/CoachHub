using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Auth.Login;

public sealed class LoginCommandHandler(
    IIdentityGateway identityGateway,
    SessionService sessions)
{
    /// <summary>Signs in and starts a new session, or returns null for invalid credentials.</summary>
    public async Task<SessionTokens?> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var user = await identityGateway.AuthenticateAsync(
            command.Login.Trim(),
            command.Password,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        return await sessions.StartAsync(user, command.Client, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string[]> Validate(LoginCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(command.Login))
        {
            errors["login"] = ["Email or user name is required."];
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            errors["password"] = ["Password is required."];
        }

        return errors;
    }
}
