using CoachHub.API.Common.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoachHub.API.Auth;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
public sealed class AuthController(LoginCommandHandler loginHandler, IAccountService accounts) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken token)
    {
        var result = await loginHandler.HandleAsync(new LoginCommand(request.Login, request.Password), token);
        return result is null ? Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials", Detail = "The login or password is incorrect." }) : Ok(result);
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

    [Authorize]
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
