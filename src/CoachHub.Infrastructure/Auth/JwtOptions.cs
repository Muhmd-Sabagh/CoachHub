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

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;
}
