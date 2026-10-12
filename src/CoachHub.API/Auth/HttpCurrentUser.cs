using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Tenancy;

namespace CoachHub.API.Auth;

/// <summary>Resolves the caller and their tenant scope from the request's JWT claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
    private bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
    public string? DisplayName => IsAuthenticated ? Principal!.Identity!.Name : null;
    public string? Role => IsAuthenticated ? Principal!.FindFirstValue(ClaimTypes.Role) : null;
    public Guid? CoachId => ClaimId(AuthClaims.CoachId);
    public Guid? ClientId => ClaimId(AuthClaims.ClientId);
    public bool IsAdministrator => IsAuthenticated && Principal!.IsInRole(AuthRoles.Administrator);

    public TenantScope Scope
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return TenantScope.Unrestricted;
            if (IsAdministrator) return TenantScope.Unrestricted;
            if (IsAuthenticated && Principal!.IsInRole(AuthRoles.Coach) && CoachId.HasValue) return TenantScope.Coach;
            if (IsAuthenticated && Principal!.IsInRole(AuthRoles.Client) && CoachId.HasValue && ClientId.HasValue)
                return TenantScope.Client;

            // The anonymous authentication surface (sign-in, refresh, password reset) is trusted
            // system work: it writes sessions and account mail for whichever account it serves.
            if (!IsAuthenticated && context.Request.Path.StartsWithSegments("/" + ApiRoutes.V1 + "/auth"))
                return TenantScope.Unrestricted;

            return TenantScope.None;
        }
    }

    private Guid? ClaimId(string type) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(type), out var id) && id != Guid.Empty ? id : null;
}
