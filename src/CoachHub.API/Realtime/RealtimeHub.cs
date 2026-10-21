using System.IdentityModel.Tokens.Jwt;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoachHub.API.Realtime;

/// <summary>
/// Live delivery (P14): new chat messages, read status and notifications, pushed to the signed-in account. Clients
/// only listen; every write goes through the authorized REST endpoints, which stay the authorization boundary.
/// </summary>
[Authorize(Policy = AuthPolicies.AnyRole)]
public sealed class RealtimeHub : Hub
{
    public const string Path = "/" + ApiRoutes.V1 + "/realtime";
}

/// <summary>SignalR addresses accounts by the token's subject (claims are not mapped to Microsoft names).</summary>
public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}

/// <summary>Pushes events to every open connection of the accounts. Best effort: a failure never fails the request.</summary>
public sealed class SignalRRealtimePublisher(IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    public async Task PublishAsync(IReadOnlyCollection<Guid> userIds, string eventName, object payload, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return;
        try
        {
            await hub.Clients.Users(userIds.Select(x => x.ToString()).ToArray()).SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Live delivery of {Event} failed.", eventName);
        }
    }
}
