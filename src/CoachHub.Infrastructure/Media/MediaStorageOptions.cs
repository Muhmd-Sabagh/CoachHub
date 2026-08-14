namespace CoachHub.Infrastructure.Media;

public sealed class MediaStorageOptions
{
    public const string SectionName = "Media";

    public string Provider { get; init; } = string.Empty;

    public string StorageRoot { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";
    public string ServiceUrl { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = "coachhub";
    public bool ForcePathStyle { get; init; }
}
