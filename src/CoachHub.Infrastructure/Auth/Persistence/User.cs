using Microsoft.AspNetCore.Identity;

namespace CoachHub.Infrastructure.Auth.Persistence;

public sealed class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Set for Coach accounts: the coach (tenant) this login belongs to.</summary>
    public Guid? CoachId { get; set; }

    /// <summary>Set for Client accounts: the client this login belongs to.</summary>
    public Guid? ClientId { get; set; }

    /// <summary>"en" or "ar" (<see cref="Application.Auth.UiLanguages"/>); null follows the browser.</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>The profile picture (§2), stored as media owned by this account.</summary>
    public Guid? AvatarMediaId { get; set; }
}
