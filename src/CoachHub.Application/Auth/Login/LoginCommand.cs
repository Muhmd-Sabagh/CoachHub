using CoachHub.Application.Auth.Sessions;

namespace CoachHub.Application.Auth.Login;

/// <summary>Login with an email address or a user name (§2).</summary>
public sealed record LoginCommand(string Login, string Password, SessionClient Client);
