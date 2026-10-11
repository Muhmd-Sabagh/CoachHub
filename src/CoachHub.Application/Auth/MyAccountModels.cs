using CoachHub.Application.Media;

namespace CoachHub.Application.Auth;

/// <summary>The interface languages (§53); right-to-left for Arabic.</summary>
public static class UiLanguages
{
    public const string English = "en";
    public const string Arabic = "ar";

    public static readonly string[] All = [English, Arabic];

    public static string? Normalize(string? language) =>
        All.SingleOrDefault(code => string.Equals(code, language?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>The signed-in account's own profile (§2: personal information and profile picture).</summary>
public sealed record MyProfileResponse(
    Guid UserId,
    string Email,
    string UserName,
    string DisplayName,
    string? PhoneNumber,
    string? PreferredLanguage,
    string Role,
    Guid? CoachId,
    Guid? ClientId,
    Guid? AvatarMediaId);

public sealed record UpdateMyProfileInput(string DisplayName, string? PhoneNumber, string? PreferredLanguage);

public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword);

/// <summary>A new profile picture, already opened by the API.</summary>
public sealed record AvatarUpload(Stream Content, string FileName, string ContentType, long SizeBytes);

/// <summary>Self-service for every role. Each call acts on the caller's own account only.</summary>
public interface IMyAccountService
{
    Task<MyProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<MyProfileResponse> UpdateProfileAsync(Guid userId, UpdateMyProfileInput input, CancellationToken cancellationToken);

    /// <summary>Changes the password and ends every other session; the current one stays signed in.</summary>
    Task ChangePasswordAsync(
        Guid userId,
        Guid? currentSessionId,
        ChangePasswordInput input,
        CancellationToken cancellationToken);

    /// <summary>Stores a new profile picture and deletes the previous one.</summary>
    Task<MediaMetadata> SetAvatarAsync(Guid userId, AvatarUpload upload, CancellationToken cancellationToken);

    Task RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken);
}
