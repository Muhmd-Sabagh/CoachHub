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
}
