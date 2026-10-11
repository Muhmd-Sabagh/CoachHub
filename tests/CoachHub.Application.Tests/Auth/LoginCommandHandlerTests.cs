using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Tests.Auth;

public sealed class LoginCommandHandlerTests
{
    private static readonly SessionClient Browser = new("Browser", "10.0.0.1");

    private static (LoginCommandHandler Handler, InMemorySessionRepository Sessions, StubIdentityGateway Identity) Create(
        AuthenticatedUser? user)
    {
        var identity = new StubIdentityGateway(user);
        var sessions = new InMemorySessionRepository();
        var service = new SessionService(sessions, identity, new StubTokenIssuer(), new SessionOptions(), TimeProvider.System);
        return (new LoginCommandHandler(identity, service), sessions, identity);
    }

    [Fact]
    public async Task Valid_credentials_start_a_session_and_issue_tokens()
    {
        var user = new AuthenticatedUser(
            Guid.NewGuid(), "admin@coachhub.test", "Administrator", [AuthRoles.Administrator], null, null);
        var (handler, sessions, identity) = Create(user);

        var result = await handler.HandleAsync(
            new LoginCommand(" admin@coachhub.test ", "Password!123", Browser),
            CancellationToken.None);

        Assert.NotNull(result);
        var session = Assert.Single(sessions.Sessions);
        Assert.Equal($"access-{session.Id:N}", result.Result.AccessToken);
        Assert.Equal(user.Id, result.Result.UserId);
        Assert.Equal(RefreshTokens.Hash(result.RefreshToken!), session.TokenHash);
        Assert.Equal("Browser", session.UserAgent);
        Assert.Equal("admin@coachhub.test", identity.ReceivedLogin);
    }

    [Fact]
    public async Task Invalid_credentials_return_null_without_a_session()
    {
        var (handler, sessions, _) = Create(null);

        var result = await handler.HandleAsync(
            new LoginCommand("admin@coachhub.test", "wrong", Browser),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(sessions.Sessions);
    }

    [Fact]
    public async Task Empty_credentials_raise_application_validation()
    {
        var (handler, _, _) = Create(null);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new LoginCommand("", "", Browser), CancellationToken.None));

        Assert.Contains("login", exception.Errors);
        Assert.Contains("password", exception.Errors);
    }
}
