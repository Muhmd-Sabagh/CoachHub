using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Auth;

namespace CoachHub.Application.Tests.Auth;

public sealed class SessionServiceTests
{
    private static readonly SessionClient Browser = new("Browser", "10.0.0.1");

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemorySessionRepository _sessions = new();
    private readonly StubIdentityGateway _identity = new(SessionTestUsers.Coach());
    private readonly StubTokenIssuer _tokens = new();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        _service = new SessionService(_sessions, _identity, _tokens, new SessionOptions(), _time);
    }

    private AuthenticatedUser User => _identity.User!;

    [Fact]
    public async Task Refresh_rotates_the_token_and_issues_an_access_token_for_the_same_session()
    {
        var started = await _service.StartAsync(User, Browser, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(15));

        var refreshed = await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.NotNull(refreshed.RefreshToken);
        Assert.NotEqual(started.RefreshToken, refreshed.RefreshToken);
        var session = Assert.Single(_sessions.Sessions);
        Assert.Equal(session.Id, _tokens.LastSessionId);
        Assert.Equal(RefreshTokens.Hash(refreshed.RefreshToken), session.TokenHash);
        Assert.Equal(_time.Now + new SessionOptions().IdleLifetime, refreshed.RefreshExpiresAt);
    }

    [Fact]
    public async Task A_concurrent_refresh_with_the_previous_token_gets_an_access_token_but_no_new_cookie()
    {
        var started = await _service.StartAsync(User, Browser, CancellationToken.None);
        var first = await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(5));

        var second = await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Null(second.RefreshToken);
        var session = Assert.Single(_sessions.Sessions);
        Assert.True(session.IsActive(_time.Now));
        Assert.Equal(RefreshTokens.Hash(first!.RefreshToken!), session.TokenHash);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_after_the_grace_period_ends_the_session()
    {
        var started = await _service.StartAsync(User, Browser, CancellationToken.None);
        var rotated = await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(5));

        var replay = await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None);

        Assert.Null(replay);
        var session = Assert.Single(_sessions.Sessions);
        Assert.Equal(SessionEndReason.TokenReuse, session.EndReason);
        // The legitimate holder is signed out too: the stolen token cannot be told apart.
        Assert.Null(await _service.RefreshAsync(rotated!.RefreshToken, Browser, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_malformed_and_expired_tokens_are_refused()
    {
        var started = await _service.StartAsync(User, Browser, CancellationToken.None);

        Assert.Null(await _service.RefreshAsync(null, Browser, CancellationToken.None));
        Assert.Null(await _service.RefreshAsync("unknown", Browser, CancellationToken.None));
        Assert.Null(await _service.RefreshAsync(new string('x', 300), Browser, CancellationToken.None));

        _time.Advance(new SessionOptions().IdleLifetime);
        Assert.Null(await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None));
    }

    [Fact]
    public async Task A_deactivated_account_cannot_refresh_and_its_session_ends()
    {
        var started = await _service.StartAsync(User, Browser, CancellationToken.None);
        _identity.User = null;

        Assert.Null(await _service.RefreshAsync(started.RefreshToken, Browser, CancellationToken.None));
        Assert.Equal(SessionEndReason.AccountDisabled, Assert.Single(_sessions.Sessions).EndReason);
    }

    [Fact]
    public async Task Sign_out_ends_only_the_callers_own_session()
    {
        var mine = await _service.StartAsync(User, Browser, CancellationToken.None);
        var mineId = _tokens.LastSessionId!.Value;

        await _service.SignOutAsync(Guid.NewGuid(), mineId, CancellationToken.None);
        Assert.NotNull(await _service.RefreshAsync(mine.RefreshToken, Browser, CancellationToken.None));

        await _service.SignOutAsync(User.Id, mineId, CancellationToken.None);
        Assert.Equal(SessionEndReason.SignedOut, Assert.Single(_sessions.Sessions).EndReason);
    }

    [Fact]
    public async Task Sessions_are_listed_current_first_and_can_be_revoked_one_by_one_or_all_but_current()
    {
        await _service.StartAsync(User, new SessionClient("Phone", null), CancellationToken.None);
        var phone = _tokens.LastSessionId!.Value;
        await _service.StartAsync(User, new SessionClient("Laptop", null), CancellationToken.None);
        var laptop = _tokens.LastSessionId!.Value;
        await _service.StartAsync(User, new SessionClient("Tablet", null), CancellationToken.None);

        var listed = await _service.ListAsync(User.Id, laptop, CancellationToken.None);
        Assert.Equal(3, listed.Count);
        Assert.True(listed[0].IsCurrent);
        Assert.Equal("Laptop", listed[0].UserAgent);

        await _service.RevokeAsync(User.Id, phone, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(User.Id, phone, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(Guid.NewGuid(), laptop, CancellationToken.None));

        await _service.RevokeAllAsync(User.Id, laptop, SessionEndReason.RevokedByUser, CancellationToken.None);
        var remaining = await _service.ListAsync(User.Id, laptop, CancellationToken.None);
        Assert.Equal(laptop, Assert.Single(remaining).Id);
    }

    [Fact]
    public void Options_reject_inconsistent_lifetimes()
    {
        Assert.Throws<InvalidOperationException>(() => new SessionOptions { IdleDays = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SessionOptions { IdleDays = 20, AbsoluteDays = 10 }.Validate());
        Assert.Throws<InvalidOperationException>(() => new SessionOptions { RotationGraceSeconds = 600 }.Validate());
        new SessionOptions().Validate();
    }

    [Fact]
    public void Refresh_tokens_are_random_url_safe_and_hashed_deterministically()
    {
        var first = RefreshTokens.Generate();
        var second = RefreshTokens.Generate();

        Assert.NotEqual(first, second);
        Assert.Equal(43, first.Length);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.Equal(RefreshTokens.Hash(first), RefreshTokens.Hash(first));
        Assert.Equal(64, RefreshTokens.Hash(first).Length);
    }
}
