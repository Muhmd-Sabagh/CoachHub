using System.ComponentModel.DataAnnotations;

namespace CoachHub.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Kept short: sessions stay signed in through the rotating refresh token.</summary>
    [Range(5, 60)]
    public int AccessTokenMinutes { get; init; } = 15;
}
