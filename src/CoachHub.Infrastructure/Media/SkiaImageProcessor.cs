using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using SkiaSharp;

namespace CoachHub.Infrastructure.Media;

/// <summary>
/// Decodes JPEG, PNG and WebP uploads and encodes them again in the same format (P20). The encoders write no
/// metadata, so EXIF (location, device), XMP and text chunks are dropped. The EXIF orientation is applied to the
/// pixels, and the longer side is capped at <see cref="MaximumDimension"/>. GIF is stored as sent: rewriting it
/// would lose animation, and it carries no EXIF.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int MaximumDimension = 2560;
    public const int MaximumMegapixels = 100;
    private const int Quality = 90;

    public bool Rewrites(string contentType) => FormatOf(contentType) is not null;

    public ProcessedImage Process(Stream content, string contentType)
    {
        var format = FormatOf(contentType)
            ?? throw new ArgumentException("This content type is not rewritten.", nameof(contentType));

        content.Position = 0;
        using var codec = SKCodec.Create(content) ?? throw Unreadable();
        var source = codec.Info;
        if ((long)source.Width * source.Height > MaximumMegapixels * 1_000_000L)
        {
            throw Invalid($"The image has more than {MaximumMegapixels} megapixels.");
        }

        var turned = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var scale = Math.Min(1f, (float)MaximumDimension / Math.Max(source.Width, source.Height));

        using var decoded = Decode(codec, scale) ?? throw Unreadable();
        var width = Math.Max(1, (int)Math.Round((turned ? source.Height : source.Width) * scale));
        var height = Math.Max(1, (int)Math.Round((turned ? source.Width : source.Height) * scale));

        var opaque = format == SKEncodedImageFormat.Jpeg;
        using var surface = SKSurface.Create(new SKImageInfo(
            width, height, SKColorType.Rgba8888, opaque ? SKAlphaType.Opaque : SKAlphaType.Premul))
            ?? throw Unreadable();
        var canvas = surface.Canvas;
        canvas.Clear(opaque ? SKColors.White : SKColors.Transparent);

        // Map the decoded pixels (possibly already reduced by the codec) onto the upright, capped canvas.
        var drawScale = (float)(turned ? height : width) / decoded.Width;
        canvas.SetMatrix(SKMatrix.CreateScale(drawScale, drawScale)
            .PreConcat(Orientation(codec.EncodedOrigin, decoded.Width, decoded.Height)));
        using var image = SKImage.FromBitmap(decoded);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(format, Quality) ?? throw Unreadable();
        return new ProcessedImage(encoded.ToArray());
    }

    private static SKEncodedImageFormat? FormatOf(string contentType) => contentType switch
    {
        "image/jpeg" => SKEncodedImageFormat.Jpeg,
        "image/png" => SKEncodedImageFormat.Png,
        "image/webp" => SKEncodedImageFormat.Webp,
        _ => null
    };

    /// <summary>Decodes at a reduced size when the codec supports it (JPEG), so very large photos stay cheap.</summary>
    private static SKBitmap? Decode(SKCodec codec, float scale)
    {
        var size = scale < 1f ? codec.GetScaledDimensions(scale) : codec.Info.Size;
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    /// <summary>The transform that turns stored pixels (w × h) upright, per the EXIF orientation.</summary>
    private static SKMatrix Orientation(SKEncodedOrigin origin, float w, float h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity
    };

    private static ValidationException Unreadable() => Invalid("The image could not be read.");

    private static ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });
}
