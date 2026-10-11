using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoachHub.Application.Auth;
using CoachHub.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CoachHub.API.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddCoachHubAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
            string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
            jwtOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT Issuer, Audience, and a SigningKey of at least 32 characters are required.");
        }

        if (jwtOptions.AccessTokenMinutes is < 5 or > 60)
        {
            throw new InvalidOperationException(
                "JWT AccessTokenMinutes must be between 5 and 60; sessions last through refresh tokens.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        // Role policies decide who may call an endpoint; which rows they see is decided by the
        // tenant query filters (see CoachHubDbContext).
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(AuthRoles.Administrator));
            options.AddPolicy(AuthPolicies.CoachOrAdmin, policy => policy.RequireRole(AuthRoles.Administrator, AuthRoles.Coach));
            options.AddPolicy(AuthPolicies.ClientOnly, policy => policy.RequireRole(AuthRoles.Client));
            options.AddPolicy(AuthPolicies.AnyRole, policy => policy.RequireRole(AuthRoles.All));
        });
        return services;
    }
}
