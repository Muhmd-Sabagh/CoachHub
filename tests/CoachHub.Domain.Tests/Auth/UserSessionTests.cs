using CoachHub.Domain.Auth;

namespace CoachHub.Domain.Tests.Auth;

public sealed class UserSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Idle = TimeSpan.FromDays(14);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(30);

    private static UserSession NewSession() =>
        UserSession.Create(Guid.NewGuid(), "hash-1", "Browser", "10.0.0.1", Start, Idle, Absolute);

    [Fact]
    public void New_session_is_active_until_the_idle_expiry()
    {
        var session = NewSession();

        Assert.True(session.IsActive(Start));
        Assert.Equal(Start + Idle, session.ExpiresAt);
        Assert.Equal(Start + Absolute, session.AbsoluteExpiresAt);
        Assert.False(session.IsActive(Start + Idle));
    }

    [Fact]
    public void Rotation_replaces_the_token_and_extends_the_idle_expiry()
    {
        var session = NewSession();
        var later = Start.AddDays(5);

        session.Rotate("hash-2", "New browser", null, later, Idle);

        Assert.Equal("hash-2", session.TokenHash);
        Assert.Equal("hash-1", session.PreviousTokenHash);
        Assert.Equal(later, session.LastSeenAt);
        Assert.Equal(later + Idle, session.ExpiresAt);
        Assert.Equal("New browser", session.UserAgent);
        Assert.Equal("10.0.0.1", session.IpAddress);
    }

    [Fact]
    public void Rotation_never_extends_past_the_absolute_expiry()
    {
        var session = NewSession();

        session.Rotate("hash-2", null, null, Start.AddDays(10), Idle);
        session.Rotate("hash-3", null, null, Start.AddDays(20), Idle);

        Assert.Equal(Start + Absolute, session.ExpiresAt);
        Assert.False(session.IsActive(Start + Absolute));
    }

    [Fact]
    public void The_previous_token_counts_as_concurrent_only_within_the_grace_period()
    {
        var session = NewSession();
        var rotatedAt = Start.AddMinutes(15);
        session.Rotate("hash-2", null, null, rotatedAt, Idle);
        var grace = TimeSpan.FromSeconds(30);

        Assert.True(session.IsRecentlyRotatedFrom("hash-1", rotatedAt.AddSeconds(10), grace));
        Assert.False(session.IsRecentlyRotatedFrom("hash-1", rotatedAt.AddSeconds(31), grace));
        Assert.False(session.IsRecentlyRotatedFrom("other", rotatedAt, grace));
    }

    [Fact]
    public void Revoked_session_is_inactive_keeps_its_first_reason_and_cannot_rotate()
    {
        var session = NewSession();

        session.Revoke(Start.AddMinutes(1), SessionEndReason.SignedOut);
        session.Revoke(Start.AddMinutes(2), SessionEndReason.TokenReuse);

        Assert.False(session.IsActive(Start.AddMinutes(3)));
        Assert.Equal(SessionEndReason.SignedOut, session.EndReason);
        Assert.Equal(Start.AddMinutes(1), session.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => session.Rotate("hash-2", null, null, Start.AddMinutes(3), Idle));
    }

    [Fact]
    public void Long_device_details_are_truncated_and_blank_ones_dropped()
    {
        var session = UserSession.Create(
            Guid.NewGuid(), "hash", new string('a', 600), "  ", Start, Idle, Absolute);

        Assert.Equal(UserSession.MaxUserAgentLength, session.UserAgent!.Length);
        Assert.Null(session.IpAddress);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => UserSession.Create(Guid.Empty, "hash", null, null, Start, Idle, Absolute));
        Assert.Throws<ArgumentException>(() => UserSession.Create(Guid.NewGuid(), " ", null, null, Start, Idle, Absolute));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UserSession.Create(Guid.NewGuid(), "hash", null, null, Start, Idle, TimeSpan.FromDays(1)));
    }
}
