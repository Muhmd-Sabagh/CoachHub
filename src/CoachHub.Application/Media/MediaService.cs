using System.Text.Json.Serialization;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Media;

namespace CoachHub.Application.Media;

/// <summary>
/// What an upload may be. Standard: images and PDF up to 20 MB. Shared (P14: files a coach shares, chat
/// attachments): also videos, Office documents and plain text, up to 50 MB.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MediaUploadPolicy>))]
public enum MediaUploadPolicy { Standard, Shared }

public sealed class MediaService(
    IMediaStorage storage,
    IMediaRepository repository)
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;
    public const long MaximumSharedFileSizeBytes = 50 * 1024 * 1024;
    private const int SignatureBytes = 512;

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "application/pdf"
    ];

    private static readonly HashSet<string> SharedContentTypes =
    [
        .. AllowedContentTypes,
        "video/mp4",
        "video/webm",
        "video/quicktime",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain"
    ];

    public Task<MediaMetadata> UploadAsync(
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        MediaOwner owner,
        CancellationToken cancellationToken) =>
        UploadAsync(content, originalFileName, contentType, sizeBytes, owner, MediaUploadPolicy.Standard, cancellationToken);

    public async Task<MediaMetadata> UploadAsync(
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        MediaOwner owner,
        MediaUploadPolicy policy,
        CancellationToken cancellationToken)
    {
        ValidateUpload(content, originalFileName, contentType, sizeBytes, policy);

        var stored = await storage.StoreAsync(
            content,
            originalFileName,
            contentType,
            cancellationToken);

        var media = MediaAsset.Create(
            stored.StorageKey,
            originalFileName,
            contentType,
            sizeBytes,
            DateTimeOffset.UtcNow,
            owner.CoachId,
            owner.ClientId,
            owner.UploadedByUserId);

        try
        {
            await repository.AddAsync(media, cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        return ToMetadata(media);
    }

    public async Task<MediaContent> OpenReadAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var media = await repository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("Media", id);

        var content = await storage.OpenReadAsync(media.StorageKey, cancellationToken);
        return new MediaContent(
            content,
            media.ContentType,
            media.OriginalFileName,
            media.SizeBytes);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await repository.FindAsync(id, cancellationToken)
            ?? throw new NotFoundException("Media", id);

        // Remove the row first: a referenced file is refused there and its content must survive.
        await repository.DeleteAsync(media, cancellationToken);
        await storage.DeleteAsync(media.StorageKey, cancellationToken);
    }

    /// <summary>
    /// Removes stored content whose rows were already deleted (for example with a deleted client).
    /// Best effort: a failure leaves an unreferenced object, never a broken row.
    /// </summary>
    public async Task DeleteContentAsync(IEnumerable<string> storageKeys, CancellationToken cancellationToken)
    {
        foreach (var key in storageKeys)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The row is gone, so nothing can reach the object any more.
            }
        }
    }

    /// <summary>Catalog entries (foods, exercises) may only show platform images, never tenant or client media.</summary>
    public static bool IsPlatformImage(Domain.Media.MediaAsset? media) =>
        media is not null && media.IsPlatformMedia &&
        media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>An image in the coach's own library (not a client's media), usable by that coach's custom catalog entries.</summary>
    public static bool IsCoachLibraryImage(Domain.Media.MediaAsset? media, Guid coachId) =>
        media is not null && media.CoachId == coachId && media.ClientId is null &&
        media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUpload(
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        MediaUploadPolicy policy)
    {
        var errors = new Dictionary<string, string[]>();
        var shared = policy == MediaUploadPolicy.Shared;
        var limit = shared ? MaximumSharedFileSizeBytes : MaximumFileSizeBytes;

        if (!content.CanRead || sizeBytes <= 0)
        {
            errors["file"] = ["A non-empty readable file is required."];
        }
        else if (sizeBytes > limit)
        {
            errors["file"] = [$"The file exceeds the {limit / (1024 * 1024)} MB limit."];
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            errors["fileName"] = ["The original file name is required."];
        }

        if (!(shared ? SharedContentTypes : AllowedContentTypes).Contains(contentType))
        {
            errors["contentType"] = [shared
                ? "Images, PDF, MP4/WebM/MOV videos, Word, Excel, PowerPoint or text files are supported."
                : "JPEG, PNG, WebP, GIF, or PDF files are supported."];
        }
        else if (content.CanRead && sizeBytes > 0 && !HasExpectedSignature(content, contentType))
        {
            errors["file"] = ["The file content does not match its declared content type."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static bool HasExpectedSignature(Stream content, string contentType)
    {
        if (!content.CanSeek)
        {
            return false;
        }

        var originalPosition = content.Position;
        Span<byte> header = stackalloc byte[SignatureBytes];
        var bytesRead = content.Read(header);
        content.Position = originalPosition;

        return contentType switch
        {
            "image/jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => bytesRead >= 8 && header[0] == 0x89 &&
                header[1..8].SequenceEqual("PNG\r\n\u001a\n"u8),
            "image/webp" => bytesRead >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WEBP"u8),
            "image/gif" => bytesRead >= 6 &&
                (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)),
            "application/pdf" => bytesRead >= 5 && header[..5].SequenceEqual("%PDF-"u8),
            // ISO base media (MP4, QuickTime): a box size, then "ftyp".
            "video/mp4" or "video/quicktime" => bytesRead >= 8 && header[4..8].SequenceEqual("ftyp"u8),
            "video/webm" => bytesRead >= 4 && header[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            // Office Open XML documents are ZIP packages.
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" =>
                bytesRead >= 4 && header[..4].SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
            // Text has no signature; a NUL byte at the start means binary content.
            "text/plain" => bytesRead > 0 && !header[..bytesRead].Contains((byte)0),
            _ => false
        };
    }

    private static MediaMetadata ToMetadata(MediaAsset media) =>
        new(media.Id, media.OriginalFileName, media.ContentType, media.SizeBytes, media.CreatedAt);
}
