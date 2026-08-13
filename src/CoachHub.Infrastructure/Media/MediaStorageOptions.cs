namespace CoachHub.Infrastructure.Media;

public sealed class MediaStorageOptions
{
    public const string SectionName = "Media";

    public string Provider { get; init; } = string.Empty;

    public string StorageRoot { get; init; } = string.Empty;
}
