using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auditing;
using CoachHub.Application.Auth;
using CoachHub.Domain.Auditing;

namespace CoachHub.API.Auditing;

/// <summary>The audit actor from the request: the signed-in user with their real role (D2).</summary>
public sealed class HttpAuditActorAccessor(IHttpContextAccessor httpContextAccessor)
    : IAuditActorAccessor
{
    public AuditActor Current
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            var user = context?.User;
            if (user?.Identity?.IsAuthenticated == true &&
                Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            {
                var kind = user.IsInRole(AuthRoles.Administrator) ? AuditActorKind.Administrator
                    : user.IsInRole(AuthRoles.Coach) ? AuditActorKind.Coach
                    : user.IsInRole(AuthRoles.Client) ? AuditActorKind.Client
                    : (AuditActorKind?)null;
                if (kind is { } role) return new AuditActor(role, userId, user.Identity.Name);
            }

            return new AuditActor(AuditActorKind.System);
        }
    }
}
