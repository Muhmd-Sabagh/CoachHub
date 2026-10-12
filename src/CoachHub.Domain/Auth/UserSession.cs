using CoachHub.Domain.Common;

namespace CoachHub.Domain.Auth;

/// <summary>Why a session stopped being usable.</summary>
public enum SessionEndReason
{
    SignedOut,
    RevokedByUser,
    PasswordChanged,
    PasswordReset,
    AccountDisabled,
    TokenReuse
}

/// <summary>
/// One signed-in device (§2 session management, §52 secure sessions). The refresh token is never
/// stored, only its hash. Every refresh rotates the token; the previous hash is kept so that a
/// replayed token can be told apart from a concurrent refresh.
/// </summary>
public sealed class UserSession : Entity
{
    public const int MaxUserAgentLength = 512;
    public const int MaxIpAddressLength = 64;

    private UserSession() { }

    public Guid UserId { get; private set; }
    [AuditIgnore] public string TokenHash { get; private set; } = string.Empty;
    [AuditIgnore] public string? PreviousTokenHash { get; private set; }
    [AuditIgnore] public DateTimeOffset? RotatedAt { get; private set; }
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    [AuditIgnore] public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>When the session expires without further use. Never later than <see cref="AbsoluteExpiresAt"/>.</summary>
    [AuditIgnore] public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The hard limit: the session ends then even if it is used every day.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public SessionEndReason? EndReason { get; private set; }

    public static UserSession Create(
        Guid userId,
        string tokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        TimeSpan idleLifetime,
        TimeSpan absoluteLifetime)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (idleLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleLifetime));
        if (absoluteLifetime < idleLifetime) throw new ArgumentOutOfRangeException(nameof(absoluteLifetime));

        var session = new UserSession
        {
            UserId = userId,
            TokenHash = tokenHash,
            UserAgent = Truncate(userAgent, MaxUserAgentLength),
            IpAddress = Truncate(ipAddress, MaxIpAddressLength),
            CreatedAt = now,
            LastSeenAt = now,
            AbsoluteExpiresAt = now + absoluteLifetime
        };
        session.ExpiresAt = session.Cap(now + idleLifetime);
        return session;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>Replaces the refresh token and extends the idle expiry, up to the absolute limit.</summary>
    public void Rotate(
        string newTokenHash,
        string? userAgent,
        string? ipAddress,
        DateTimeOffset now,
        TimeSpan idleLifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newTokenHash);
        if (!IsActive(now)) throw new InvalidOperationException("An ended session cannot be refreshed.");

        PreviousTokenHash = TokenHash;
        TokenHash = newTokenHash;
        RotatedAt = now;
        LastSeenAt = now;
        UserAgent = Truncate(userAgent, MaxUserAgentLength) ?? UserAgent;
        IpAddress = Truncate(ipAddress, MaxIpAddressLength) ?? IpAddress;
        ExpiresAt = Cap(now + idleLifetime);
    }

    /// <summary>
    /// True when <paramref name="tokenHash"/> is the token this session rotated away from moments
    /// ago: another tab refreshed at the same time. Outside the grace period it is a replay.
    /// </summary>
    public bool IsRecentlyRotatedFrom(string tokenHash, DateTimeOffset now, TimeSpan grace) =>
        PreviousTokenHash == tokenHash && RotatedAt is { } rotatedAt && now - rotatedAt <= grace;

    /// <summary>Ends the session. Ending an ended session keeps the first reason.</summary>
    public void Revoke(DateTimeOffset now, SessionEndReason reason)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        EndReason = reason;
    }

    private DateTimeOffset Cap(DateTimeOffset expiresAt) =>
        expiresAt < AbsoluteExpiresAt ? expiresAt : AbsoluteExpiresAt;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
