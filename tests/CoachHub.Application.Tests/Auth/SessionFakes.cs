using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Domain.Auth;

namespace CoachHub.Application.Tests.Auth;

internal sealed class InMemorySessionRepository : IUserSessionRepository
{
    public List<UserSession> Sessions { get; } = [];
    public int Saves { get; private set; }

    public Task AddAsync(UserSession session, CancellationToken cancellationToken)
    {
        Sessions.Add(session);
        Saves++;
        return Task.CompletedTask;
    }

    public Task<UserSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(
            Sessions.FirstOrDefault(session => session.TokenHash == tokenHash) ??
            Sessions.FirstOrDefault(session => session.PreviousTokenHash == tokenHash));

    public Task<UserSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.SingleOrDefault(session => session.Id == id));

    public Task<IReadOnlyList<UserSession>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserSession>>(
            Sessions.Where(session => session.UserId == userId && session.IsActive(now)).ToArray());

    public Task RevokeAllAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionEndReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var session in Sessions.Where(session =>
                     session.UserId == userId && session.Id != exceptSessionId && session.IsActive(now)))
        {
            session.Revoke(now, reason);
        }
        Saves++;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class StubIdentityGateway(AuthenticatedUser? user) : IIdentityGateway
{
    public AuthenticatedUser? User { get; set; } = user;
    public string? ReceivedLogin { get; private set; }

    public Task<AuthenticatedUser?> AuthenticateAsync(string login, string password, CancellationToken cancellationToken)
    {
        ReceivedLogin = login;
        return Task.FromResult(User);
    }

    public Task<AuthenticatedUser?> FindActiveAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(User?.Id == userId ? User : null);
}

internal sealed class StubTokenIssuer : ITokenIssuer
{
    public Guid? LastSessionId { get; private set; }

    public IssuedToken Issue(AuthenticatedUser user, Guid sessionId)
    {
        LastSessionId = sessionId;
        return new IssuedToken($"access-{sessionId:N}", DateTimeOffset.UtcNow.AddMinutes(15));
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

internal static class SessionTestUsers
{
    public static AuthenticatedUser Coach() => new(
        Guid.NewGuid(), "coach@coachhub.test", "Coach", [AuthRoles.Coach], Guid.NewGuid(), null);
}
