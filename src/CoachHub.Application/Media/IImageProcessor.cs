namespace CoachHub.Application.Media;

/// <summary>
/// Rewrites uploaded images before they are stored (§26, §52, P20): metadata such as EXIF location is dropped,
/// the orientation is applied to the pixels and the dimensions are capped. The content type stays the same.
/// </summary>
public interface IImageProcessor
{
    /// <summary>Whether uploads of this content type are rewritten. Others are stored as sent.</summary>
    bool Rewrites(string contentType);

    /// <summary>
    /// The rewritten image. Throws <see cref="Common.Exceptions.ValidationException"/> (field <c>file</c>) when the
    /// content cannot be decoded or has too many pixels.
    /// </summary>
    ProcessedImage Process(Stream content, string contentType);
}

public sealed record ProcessedImage(byte[] Content);
