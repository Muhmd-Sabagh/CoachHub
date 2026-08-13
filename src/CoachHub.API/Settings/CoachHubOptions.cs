using System.ComponentModel.DataAnnotations;

namespace CoachHub.API.Settings;

public sealed class CoachHubOptions
{
    public const string SectionName = "CoachHub";

    [Required]
    [MinLength(2)]
    public string CoachName { get; init; } = string.Empty;
}
