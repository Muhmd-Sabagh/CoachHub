namespace CoachHub.Infrastructure.Auth;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "Authentication:BootstrapAdmin";

    public bool Enabled { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "Administrator";
}
