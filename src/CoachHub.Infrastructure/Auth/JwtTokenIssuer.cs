using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoachHub.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoachHub.Infrastructure.Auth;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    public IssuedToken Issue(AuthenticatedUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Permissions.Select(permission => new Claim(AuthPermissions.ClaimType, permission)));
        if (user.ClientId.HasValue) claims.Add(new Claim("client_id", user.ClientId.Value.ToString()));
        var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer: _options.Issuer, audience: _options.Audience, claims: claims, notBefore: DateTime.UtcNow, expires: expiresAt.UtcDateTime, signingCredentials: signingCredentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
