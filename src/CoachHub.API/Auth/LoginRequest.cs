using System.ComponentModel.DataAnnotations;

namespace CoachHub.API.Auth;

public sealed class LoginRequest
{
    /// <summary>The account's email address or user name (§2).</summary>
    [Required]
    public string Login { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
