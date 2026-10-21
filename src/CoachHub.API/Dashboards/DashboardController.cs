using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Dashboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Dashboards;

/// <summary>The coach dashboard (§38) and the administrator dashboard (§48), both reporting revenue (D6).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/dashboard")]
public sealed class DashboardController(DashboardService dashboards) : ControllerBase
{
    /// <summary>The coach's clients, review queue, check-ins, expired programs, recent activity and revenue.</summary>
    [HttpGet("coach")]
    public Task<CoachDashboard> Coach([FromQuery] DashboardQuery query, CancellationToken token) => dashboards.CoachAsync(query, token);

    /// <summary>Platform counts, users, expired subscriptions, revenue per currency and coach, activity and usage.</summary>
    [HttpGet("admin")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<AdminDashboard> Admin([FromQuery] DashboardQuery query, CancellationToken token) => dashboards.AdminAsync(query, token);
}
