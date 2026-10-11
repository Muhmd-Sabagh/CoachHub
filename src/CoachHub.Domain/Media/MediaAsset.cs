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
            FileNameOnly(originalFileName),
            contentType,
            sizeBytes,
            createdAt);
    }

    // Browsers on Windows may send "C:\dir\name.png"; Path.GetFileName ignores '\' on Linux,
    // so strip both separators regardless of the host OS.
    private static string FileNameOnly(string originalFileName)
    {
        var name = originalFileName[(originalFileName.LastIndexOfAny(['/', '\\']) + 1)..];
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(originalFileName));
        return name;
    }
}
