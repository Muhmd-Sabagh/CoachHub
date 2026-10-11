using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Auth.Login;

public sealed class LoginCommandHandler(
    IIdentityGateway identityGateway,
    ITokenIssuer tokenIssuer)
{
    public async Task<LoginResult?> HandleAsync(
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

        var token = tokenIssuer.Issue(user);

        return new LoginResult(
            token.AccessToken,
            token.ExpiresAt,
            user.Id,
            user.Email,
            user.DisplayName,
            user.Roles,
            user.CoachId,
            user.ClientId);
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
