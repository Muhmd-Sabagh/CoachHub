using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Media;

namespace CoachHub.Application.Media;

public sealed class MediaService(
    IMediaStorage storage,
    IMediaRepository repository)
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "application/pdf"
    ];

    public async Task<MediaMetadata> UploadAsync(
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        ValidateUpload(content, originalFileName, contentType, sizeBytes);

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
            DateTimeOffset.UtcNow);

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

        await storage.DeleteAsync(media.StorageKey, cancellationToken);
        await repository.DeleteAsync(media, cancellationToken);
    }

    private static void ValidateUpload(
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes)
    {
        var errors = new Dictionary<string, string[]>();

        if (!content.CanRead || sizeBytes <= 0)
        {
            errors["file"] = ["A non-empty readable file is required."];
        }
        else if (sizeBytes > MaximumFileSizeBytes)
        {
            errors["file"] = ["The file exceeds the 20 MB limit."];
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            errors["fileName"] = ["The original file name is required."];
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            errors["contentType"] = ["JPEG, PNG, WebP, GIF, or PDF files are supported."];
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
        Span<byte> header = stackalloc byte[12];
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
            _ => false
        };
    }

    private static MediaMetadata ToMetadata(MediaAsset media) =>
        new(media.Id, media.OriginalFileName, media.ContentType, media.SizeBytes, media.CreatedAt);
}
