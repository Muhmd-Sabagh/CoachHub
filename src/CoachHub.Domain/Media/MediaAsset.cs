using CoachHub.Domain.Common;

namespace CoachHub.Domain.Media;

public sealed class MediaAsset : Entity
{
    private MediaAsset()
    {
    }

    private MediaAsset(
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt)
    {
        StorageKey = storageKey;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAt = createdAt;
    }

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaAsset Create(
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new MediaAsset(
            storageKey,
            Path.GetFileName(originalFileName),
            contentType,
            sizeBytes,
            createdAt);
    }
}
