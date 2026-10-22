using CoachHub.Domain.Settings;
using CoachHub.API.Auth;
using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Calendar;
using CoachHub.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Calendar;

/// <summary>
/// The coach calendar (§46): check-ins, program start and end dates, client events and follow-ups across clients,
/// and the coach's events. Only coaches add events (owner decision 2026-10-22).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/calendar")]
public sealed class CalendarController(CalendarService calendar) : ControllerBase
{
    /// <summary>At most 62 days; <paramref name="clientId"/> narrows it to one client.</summary>
    [HttpGet]
    public Task<CoachCalendarResponse> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? clientId, CancellationToken token) =>
        calendar.CoachAsync(from, to, clientId, token);

    [HttpPost("events")]
    public Task<CalendarEventResponse> Create(CalendarEventInput input, CancellationToken token) => calendar.CreateEventAsync(input, token);

    [HttpPut("events/{id:guid}")]
    public Task<CalendarEventResponse> Update(Guid id, CalendarEventInput input, CancellationToken token) => calendar.UpdateEventAsync(id, input, token);

    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken token)
    {
        await calendar.DeleteEventAsync(id, token);
        return NoContent();
    }
}

/// <summary>A client's calendar as the coach sees and edits it (§45), including the training weekdays.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/calendar")]
public sealed class ClientCalendarController(CalendarService calendar) : ControllerBase
{
    [HttpGet]
    public Task<ClientCalendarResponse> Get(Guid clientId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token) =>
        calendar.ClientAsync(clientId, from, to, asClient: false, token);

    /// <summary>The weekdays the workout rotation fills, Monday first; the other days are rest days.</summary>
    [HttpPut("training-days")]
    public Task<ClientCalendarResponse> SetTrainingDays(Guid clientId, TrainingDaysInput input, CancellationToken token) =>
        calendar.SetTrainingDaysAsync(clientId, input, token);
}

/// <summary>The signed-in client's calendar (§45): read-only, without the coach's private entries.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/calendar")]
[ClientFeature(ClientFeature.Calendar)]
public sealed class MyCalendarController(CalendarService calendar) : ControllerBase
{
    [HttpGet]
    public Task<ClientCalendarResponse> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken token)
    {
        var clientId = Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
        return calendar.ClientAsync(clientId, from, to, asClient: true, token);
    }
}
