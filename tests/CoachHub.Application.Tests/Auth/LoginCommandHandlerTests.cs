using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Tests.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Valid_credentials_return_an_issued_token()
    {
        var user = new AuthenticatedUser(
            Guid.NewGuid(),
            "admin@coachhub.test",
            "Administrator",
            [AuthRoles.Administrator],
            [],
            null);
        var identity = new StubIdentityGateway(user);
        var issuer = new StubTokenIssuer();
        var handler = new LoginCommandHandler(identity, issuer);

        var result = await handler.HandleAsync(
            new LoginCommand(" admin@coachhub.test ", "Password!123"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("admin@coachhub.test", identity.ReceivedEmail);
    }

    [Fact]
    public async Task Invalid_credentials_return_null()
    {
        var handler = new LoginCommandHandler(
            new StubIdentityGateway(null),
            new StubTokenIssuer());

        var result = await handler.HandleAsync(
            new LoginCommand("admin@coachhub.test", "wrong"),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Empty_credentials_raise_application_validation()
    {
        var handler = new LoginCommandHandler(
            new StubIdentityGateway(null),
            new StubTokenIssuer());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(
                new LoginCommand("", ""),
                CancellationToken.None));

        Assert.Contains("email", exception.Errors);
        Assert.Contains("password", exception.Errors);
    }

    private sealed class StubIdentityGateway(AuthenticatedUser? user) : IIdentityGateway
    {
        public string? ReceivedEmail { get; private set; }

        public Task<AuthenticatedUser?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            ReceivedEmail = email;
            return Task.FromResult(user);
        }
    }

    private sealed class StubTokenIssuer : ITokenIssuer
    {
        public IssuedToken Issue(AuthenticatedUser user)
        {
            return new IssuedToken(
                "access-token",
                DateTimeOffset.UtcNow.AddHours(1));
        }
    }
}
