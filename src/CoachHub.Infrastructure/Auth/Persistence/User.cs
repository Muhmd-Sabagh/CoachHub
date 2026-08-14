using Microsoft.AspNetCore.Identity;

namespace CoachHub.Infrastructure.Auth.Persistence;

public sealed class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    public Guid? ClientId { get; set; }
}
