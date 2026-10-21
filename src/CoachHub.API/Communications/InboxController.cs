using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Communications;

/// <summary>
/// The signed-in account's in-app notifications (§42) and email preferences. Self-service: always the account in the
/// token.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AnyRole)]
[Route(ApiRoutes.V1 + "/inbox")]
public sealed class InboxController(InboxService inbox) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<InboxItem>> List([FromQuery] InboxQuery query, CancellationToken token) => inbox.ListAsync(query, token);

    [HttpGet("unread-count")]
    public Task<UnreadCountResponse> UnreadCount(CancellationToken token) => inbox.UnreadCountAsync(token);

    [HttpPost("{id:guid}/read")]
    public Task<InboxItem> Read(Guid id, CancellationToken token) => inbox.MarkReadAsync(id, token);

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken token)
    {
        await inbox.MarkAllReadAsync(token);
        return NoContent();
    }

    [HttpGet("preferences")]
    public Task<NotificationPreferencesResponse> Preferences(CancellationToken token) => inbox.PreferencesAsync(token);

    /// <summary>The events to also receive by email; in-app notifications always arrive.</summary>
    [HttpPut("preferences")]
    public Task<NotificationPreferencesResponse> SetPreferences(NotificationPreferencesInput input, CancellationToken token) =>
        inbox.SetPreferencesAsync(input, token);
}
