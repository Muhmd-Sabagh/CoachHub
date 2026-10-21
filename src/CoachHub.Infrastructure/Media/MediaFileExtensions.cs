namespace CoachHub.Infrastructure.Media;

/// <summary>The stored file extension of each accepted content type (MediaService's allowlists).</summary>
internal static class MediaFileExtensions
{
    public static string For(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "application/pdf" => ".pdf",
        // Shared files and chat attachments (P14).
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
        "text/plain" => ".txt",
        _ => throw new InvalidOperationException("Unsupported media content type.")
    };
}
