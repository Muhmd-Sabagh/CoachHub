using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Auth;

namespace CoachHub.Application.Auth.Sessions;

/// <summary>
/// Secure sessions (§2, §52): short-lived access tokens plus a rotating refresh token per device.
/// A refresh token that is replayed after it was rotated ends the whole session, because only a
/// copied token can be presented twice.
/// </summary>
public sealed class SessionService(
    IUserSessionRepository sessions,
    IIdentityGateway identity,
    ITokenIssuer tokens,
    SessionOptions options,
    TimeProvider timeProvider)
{
    public async Task<SessionTokens> StartAsync(
        AuthenticatedUser user,
        SessionClient client,
        CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokens.Generate();
        var session = UserSession.Create(
            user.Id,
            RefreshTokens.Hash(refreshToken),
            client.UserAgent,
            client.IpAddress,
            timeProvider.GetUtcNow(),
            options.IdleLifetime,
            options.AbsoluteLifetime);
        await sessions.AddAsync(session, cancellationToken);
        return Issue(user, session, refreshToken);
    }

    /// <summary>Returns new tokens, or null when the refresh token is unknown, ended, or replayed.</summary>
    public async Task<SessionTokens?> RefreshAsync(
        string? refreshToken,
        SessionClient client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256) return null;

        var now = timeProvider.GetUtcNow();
        var hash = RefreshTokens.Hash(refreshToken);
        var session = await sessions.FindByTokenHashAsync(hash, cancellationToken);
        if (session is null || !session.IsActive(now)) return null;

        var rotatedAway = session.TokenHash != hash;
        if (rotatedAway && !session.IsRecentlyRotatedFrom(hash, now, options.RotationGrace))
        {
            session.Revoke(now, SessionEndReason.TokenReuse);
            await sessions.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await identity.FindActiveAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            session.Revoke(now, SessionEndReason.AccountDisabled);
            await sessions.SaveChangesAsync(cancellationToken);
            return null;
        }

        // A concurrent refresh already rotated the token and set the new cookie; this request
        // only needs an access token.
        if (rotatedAway) return Issue(user, session, refreshToken: null);

        var nextToken = RefreshTokens.Generate();
        session.Rotate(RefreshTokens.Hash(nextToken), client.UserAgent, client.IpAddress, now, options.IdleLifetime);
        await sessions.SaveChangesAsync(cancellationToken);
        return Issue(user, session, nextToken);
    }

    /// <summary>Signs out the caller's current session. Unknown or already ended sessions are ignored.</summary>
    public async Task SignOutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.FindAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId) return;
        session.Revoke(timeProvider.GetUtcNow(), SessionEndReason.SignedOut);
        await sessions.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListAsync(
        Guid userId,
        Guid? currentSessionId,
        CancellationToken cancellationToken)
    {
        var active = await sessions.ListActiveAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return active
            .OrderByDescending(session => session.Id == currentSessionId)
            .ThenByDescending(session => session.LastSeenAt)
            .Select(session => new SessionResponse(
                session.Id,
                session.UserAgent,
                session.IpAddress,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.Id == currentSessionId))
            .ToArray();
    }

    /// <summary>Ends one of the caller's own sessions (signing out another device).</summary>
    public async Task RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = await sessions.FindAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId || !session.IsActive(now))
            throw new NotFoundException("Session", sessionId);
        session.Revoke(now, SessionEndReason.RevokedByUser);
        await sessions.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ends every session of the user except <paramref name="keepSessionId"/>.</summary>
    public Task RevokeAllAsync(
        Guid userId,
        Guid? keepSessionId,
        SessionEndReason reason,
        CancellationToken cancellationToken) =>
        sessions.RevokeAllAsync(userId, keepSessionId, reason, timeProvider.GetUtcNow(), cancellationToken);

    private SessionTokens Issue(AuthenticatedUser user, UserSession session, string? refreshToken)
    {
        var access = tokens.Issue(user, session.Id);
        return new SessionTokens(
            new LoginResult(
                access.AccessToken,
                access.ExpiresAt,
                user.Id,
                user.Email,
                user.DisplayName,
                user.Roles,
                user.CoachId,
                user.ClientId,
                user.PreferredLanguage),
            refreshToken,
            session.ExpiresAt);
    }
}
