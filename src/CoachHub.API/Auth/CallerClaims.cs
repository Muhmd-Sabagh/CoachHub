using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.API.Auth;

internal static class CallerClaims
{
    public static Guid RequiredUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The token does not identify an account.");

    public static Guid? SessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(AuthClaims.SessionId), out var id) && id != Guid.Empty ? id : null;

    public static SessionClient SessionClient(this HttpContext context) =>
        new(context.Request.Headers.UserAgent.ToString(), context.Connection.RemoteIpAddress?.ToString());
}
