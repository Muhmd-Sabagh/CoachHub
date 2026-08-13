using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(LoginCommandHandler loginHandler) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResult>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "The email or password is incorrect."
            });
        }

        return Ok(result);
    }

    [Authorize(Roles = AuthRoles.Administrator)]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<CurrentUserResponse> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        return Ok(new CurrentUserResponse(userId, User.Identity?.Name, roles));
    }
}
