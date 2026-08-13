using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auditing;
using CoachHub.Domain.Auditing;

namespace CoachHub.API.Auditing;

public sealed class HttpAuditActorAccessor(IHttpContextAccessor httpContextAccessor)
    : IAuditActorAccessor
{
    public AuditActor Current
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated == true)
            {
                var userIdValue = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                return new AuditActor(
                    AuditActorKind.Administrator,
                    Guid.TryParse(userIdValue, out var userId) ? userId : null,
                    context.User.Identity.Name);
            }

            if (context?.Request.Path.StartsWithSegments("/api/client-forms") == true)
            {
                return new AuditActor(AuditActorKind.PublicClient);
            }

            return new AuditActor(AuditActorKind.System);
        }
    }
}
