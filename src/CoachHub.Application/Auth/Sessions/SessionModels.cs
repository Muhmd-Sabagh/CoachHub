using CoachHub.Application.Auth.Login;
using CoachHub.Domain.Auth;

namespace CoachHub.Application.Auth.Sessions;

/// <summary>Refresh-session lifetimes, bound from <c>Authentication:Sessions</c>.</summary>
public sealed class SessionOptions
{
    public const string SectionName = "Authentication:Sessions";

    /// <summary>A session ends after this many days without a refresh.</summary>
    public int IdleDays { get; init; } = 14;

    /// <summary>A session ends this many days after sign-in, however often it is used.</summary>
    public int AbsoluteDays { get; init; } = 30;

    /// <summary>
    /// How long the token a session just rotated away from is still accepted (without rotating
    /// again), so two tabs refreshing together are not mistaken for a stolen token.
    /// </summary>
    public int RotationGraceSeconds { get; init; } = 30;

    public TimeSpan IdleLifetime => TimeSpan.FromDays(IdleDays);
    public TimeSpan AbsoluteLifetime => TimeSpan.FromDays(AbsoluteDays);
    public TimeSpan RotationGrace => TimeSpan.FromSeconds(RotationGraceSeconds);

    public void Validate()
    {
        if (IdleDays is < 1 or > 90) throw new InvalidOperationException("Sessions:IdleDays must be between 1 and 90.");
        if (AbsoluteDays < IdleDays || AbsoluteDays > 365)
            throw new InvalidOperationException("Sessions:AbsoluteDays must be at least IdleDays and at most 365.");
        if (RotationGraceSeconds is < 0 or > 120)
            throw new InvalidOperationException("Sessions:RotationGraceSeconds must be between 0 and 120.");
    }
}

/// <summary>The device a request comes from, recorded on the session for the session list.</summary>
public sealed record SessionClient(string? UserAgent, string? IpAddress);

/// <summary>
/// A signed-in session: the response body for the browser plus the refresh token, which the API
/// hands over only as an HttpOnly cookie.
/// </summary>
/// <param name="RefreshToken">
/// Null when a concurrent refresh already rotated the token: the caller keeps the cookie it has.
/// </param>
public sealed record SessionTokens(LoginResult Result, string? RefreshToken, DateTimeOffset RefreshExpiresAt);

public sealed record SessionResponse(
    Guid Id,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);

public interface IUserSessionRepository
{
    Task AddAsync(UserSession session, CancellationToken cancellationToken);

    /// <summary>The session whose current or previous refresh token has this hash.</summary>
    Task<UserSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<UserSession?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Revokes every active session of the user, except <paramref name="exceptSessionId"/>.</summary>
    Task RevokeAllAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionEndReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
