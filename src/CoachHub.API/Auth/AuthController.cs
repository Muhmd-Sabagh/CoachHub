using CoachHub.API.Common.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Auth.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoachHub.API.Auth;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
public sealed class AuthController(
    LoginCommandHandler loginHandler,
    SessionService sessions,
    IAccountService accounts) : ControllerBase
{
    /// <summary>Signs in: returns the access token and sets the refresh cookie.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken token)
    {
        var session = await loginHandler.HandleAsync(
            new LoginCommand(request.Login, request.Password, HttpContext.SessionClient()), token);
        if (session is null)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials", Detail = "The login or password is incorrect." });

        RefreshCookie.Write(Response, session.RefreshToken!, session.RefreshExpiresAt);
        return Ok(session.Result);
    }

    /// <summary>
    /// Exchanges the refresh cookie for a new access token and rotates the cookie. Anonymous by
    /// design: it is called exactly when the access token has expired.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("session-refresh")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResult>> Refresh(CancellationToken token)
    {
        var session = await sessions.RefreshAsync(RefreshCookie.Read(Request), HttpContext.SessionClient(), token);
        if (session is null)
        {
            RefreshCookie.Clear(Response);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Session ended", Detail = "Sign in again." });
        }

        if (session.RefreshToken is not null)
            RefreshCookie.Write(Response, session.RefreshToken, session.RefreshExpiresAt);
        return Ok(session.Result);
    }

    /// <summary>Ends the current session on the server and removes the refresh cookie.</summary>
    [Authorize(Policy = AuthPolicies.AnyRole)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken token)
    {
        if (User.SessionId() is { } sessionId)
            await sessions.SignOutAsync(User.RequiredUserId(), sessionId, token);
        RefreshCookie.Clear(Response);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestReset(PasswordResetRequest request, CancellationToken token)
    {
        await accounts.RequestPasswordResetAsync(request.Email, token);
        return Accepted();
    }

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("password-reset/complete")]
    public async Task<IActionResult> Reset(PasswordResetInput input, CancellationToken token)
    {
        await accounts.ResetPasswordAsync(input, token);
        return NoContent();
    }

    [Authorize(Policy = AuthPolicies.AnyRole)]
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var coachId = Guid.TryParse(User.FindFirstValue(AuthClaims.CoachId), out var coach) ? coach : (Guid?)null;
        var clientId = Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var client) ? client : (Guid?)null;
        return Ok(new CurrentUserResponse(userId, User.Identity?.Name, roles, coachId, clientId));
    }
}
