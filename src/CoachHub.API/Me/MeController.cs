using CoachHub.API.Auth;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Media;
using CoachHub.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The signed-in account's own settings (§2): profile, password, profile picture and sessions.
/// Every action works on the caller's account from the token, never on an ID in the request.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.AnyRole)]
[Route(ApiRoutes.V1 + "/me")]
public sealed class MeController(IMyAccountService account, SessionService sessions) : ControllerBase
{
    [HttpGet("profile")]
    public Task<MyProfileResponse> Profile(CancellationToken token) =>
        account.GetProfileAsync(User.RequiredUserId(), token);

    [HttpPut("profile")]
    public Task<MyProfileResponse> UpdateProfile(UpdateMyProfileInput input, CancellationToken token) =>
        account.UpdateProfileAsync(User.RequiredUserId(), input, token);

    /// <summary>Changes the password. Every other session is signed out; this one stays.</summary>
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input, CancellationToken token)
    {
        await account.ChangePasswordAsync(User.RequiredUserId(), User.SessionId(), input, token);
        return NoContent();
    }

    [HttpPut("avatar")]
    [RequestSizeLimit(MediaService.MaximumFileSizeBytes)]
    public async Task<ActionResult<MediaMetadata>> SetAvatar([FromForm] IFormFile file, CancellationToken token)
    {
        await using var content = file.OpenReadStream();
        return await account.SetAvatarAsync(
            User.RequiredUserId(),
            new AvatarUpload(content, file.FileName, file.ContentType, file.Length),
            token);
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken token)
    {
        await account.RemoveAvatarAsync(User.RequiredUserId(), token);
        return NoContent();
    }

    /// <summary>The caller's signed-in devices; the current one is flagged.</summary>
    [HttpGet("sessions")]
    public Task<IReadOnlyList<SessionResponse>> Sessions(CancellationToken token) =>
        sessions.ListAsync(User.RequiredUserId(), User.SessionId(), token);

    /// <summary>Signs out one device.</summary>
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken token)
    {
        await sessions.RevokeAsync(User.RequiredUserId(), id, token);
        return NoContent();
    }

    /// <summary>Signs out every device except this one.</summary>
    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken token)
    {
        await sessions.RevokeAllAsync(User.RequiredUserId(), User.SessionId(), SessionEndReason.RevokedByUser, token);
        return NoContent();
    }
}
