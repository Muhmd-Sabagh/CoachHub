using System.Text.RegularExpressions;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Auth;

/// <summary>Self-service for the signed-in account (§2): profile, password, profile picture.</summary>
public sealed partial class MyAccountService(
    UserManager<User> users,
    CoachHubDbContext db,
    MediaService media,
    SessionService sessions) : IMyAccountService
{
    public const long MaximumAvatarBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AvatarContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<MyProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await MapAsync(await FindAsync(userId, cancellationToken));

    public async Task<MyProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateMyProfileInput input,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var displayName = input.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is 0 or > 200) errors["displayName"] = ["A display name of at most 200 characters is required."];
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone is not null && !PhonePattern().IsMatch(phone))
            errors["phoneNumber"] = ["Use digits, spaces, '+', '-' or brackets, at most 30 characters."];
        var language = UiLanguages.Normalize(input.PreferredLanguage);
        if (!string.IsNullOrWhiteSpace(input.PreferredLanguage) && language is null)
            errors["preferredLanguage"] = ["Choose English (en) or Arabic (ar)."];
        if (errors.Count > 0) throw new ValidationException(errors);

        var user = await FindAsync(userId, cancellationToken);
        user.DisplayName = displayName;
        user.PhoneNumber = phone;
        user.PreferredLanguage = language;

        // A coach's login and their tenant carry the same name (see coach administration).
        if (user.CoachId is { } coachId &&
            await db.Set<Coach>().SingleOrDefaultAsync(coach => coach.Id == coachId, cancellationToken) is { } coach)
        {
            coach.Update(displayName, coach.IsActive);
        }

        AccountRules.Ensure(await users.UpdateAsync(user));
        return await MapAsync(user);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        Guid? currentSessionId,
        ChangePasswordInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(input.CurrentPassword) || string.IsNullOrEmpty(input.NewPassword))
            throw Validation("password", "The current and the new password are required.");

        var user = await FindAsync(userId, cancellationToken);
        if (!await users.CheckPasswordAsync(user, input.CurrentPassword))
        {
            // Counts towards lockout, so a borrowed session cannot be used to guess the password.
            await users.AccessFailedAsync(user);
            throw Validation("currentPassword", "The current password is incorrect.");
        }

        AccountRules.Ensure(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword));
        await sessions.RevokeAllAsync(userId, currentSessionId, SessionEndReason.PasswordChanged, cancellationToken);
    }

    public async Task<MediaMetadata> SetAvatarAsync(Guid userId, AvatarUpload upload, CancellationToken cancellationToken)
    {
        if (!AvatarContentTypes.Contains(upload.ContentType))
            throw Validation("file", "Use a JPEG, PNG or WebP image.");
        if (upload.SizeBytes > MaximumAvatarBytes)
            throw Validation("file", "The profile picture exceeds the 5 MB limit.");

        var user = await FindAsync(userId, cancellationToken);
        var owner = await AvatarOwnerAsync(user, cancellationToken);
        var stored = await media.UploadAsync(
            upload.Content, upload.FileName, upload.ContentType, upload.SizeBytes, owner, cancellationToken);

        var previous = user.AvatarMediaId;
        user.AvatarMediaId = stored.Id;
        try
        {
            AccountRules.Ensure(await users.UpdateAsync(user));
        }
        catch
        {
            await media.DeleteAsync(stored.Id, CancellationToken.None);
            throw;
        }

        if (previous is { } previousId) await DeleteQuietlyAsync(previousId, cancellationToken);
        return stored;
    }

    public async Task RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user.AvatarMediaId is not { } avatarId) return;
        user.AvatarMediaId = null;
        AccountRules.Ensure(await users.UpdateAsync(user));
        await DeleteQuietlyAsync(avatarId, cancellationToken);
    }

    /// <summary>
    /// Administrators' pictures are platform media, a coach's belongs to their library, and a
    /// client's to the client, so each is visible exactly where the account is.
    /// </summary>
    private async Task<MediaOwner> AvatarOwnerAsync(User user, CancellationToken cancellationToken)
    {
        if (user.CoachId is { } coachId) return MediaOwner.CoachLibrary(coachId, user.Id);
        if (user.ClientId is { } clientId)
        {
            // Identity lookup of the client's current coach; the save guard still requires the
            // caller's token to name that coach.
            var clientCoachId = await db.Set<Client>().IgnoreQueryFilters().AsNoTracking()
                .Where(client => client.Id == clientId)
                .Select(client => (Guid?)client.CoachId)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new ForbiddenException("The client account is not linked to a client.");
            return MediaOwner.OfClient(clientCoachId, clientId, user.Id);
        }
        return MediaOwner.Platform(user.Id);
    }

    private async Task DeleteQuietlyAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        try
        {
            await media.DeleteAsync(mediaId, cancellationToken);
        }
        catch (Exception exception) when (exception is NotFoundException or ConflictException)
        {
            // Already gone, or still referenced elsewhere: leave it.
        }
    }

    private async Task<User> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } ? user : throw new NotFoundException("Account", userId);
    }

    private async Task<MyProfileResponse> MapAsync(User user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.UserName ?? string.Empty,
        user.DisplayName,
        user.PhoneNumber,
        user.PreferredLanguage,
        (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty,
        user.CoachId,
        user.ClientId,
        user.AvatarMediaId);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    [GeneratedRegex(@"^[0-9+()\- ]{3,30}$")]
    private static partial Regex PhonePattern();
}
