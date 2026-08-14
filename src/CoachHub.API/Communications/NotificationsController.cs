using CoachHub.Application.Auth;
using CoachHub.Application.Communications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Communications;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AuthPermissions.ManageCommunications)]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<NotificationResponse>> List(CancellationToken token) => service.ListAsync(token);
    [HttpPost] public async Task<ActionResult<NotificationResponse>> Schedule(NotificationInput input, CancellationToken token) { var created = await service.ScheduleAsync(input, token); return Created($"/api/notifications/{created.Id}", created); }
    [HttpPost("dispatch")] public async Task<ActionResult<object>> Dispatch(CancellationToken token) => Ok(new { sent = await service.DispatchDueAsync(token) });
    [HttpPost("{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, CancellationToken token) { await service.CancelAsync(id, token); return NoContent(); }
}
