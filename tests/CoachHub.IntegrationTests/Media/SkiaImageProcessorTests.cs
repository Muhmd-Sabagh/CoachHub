using CoachHub.Application.Common.Exceptions;
using CoachHub.Infrastructure.Media;
using SkiaSharp;

namespace CoachHub.IntegrationTests.Media;

public sealed class SkiaImageProcessorTests
{
    private readonly SkiaImageProcessor _processor = new();

    [Fact]
    public void A_photo_loses_its_exif_and_is_stored_upright()
    {
        // 40 × 20: red on the left, blue on the right, tagged "rotate 90° clockwise" with a GPS block.
        var jpeg = WithExif(Encode(Halves(40, 20), SKEncodedImageFormat.Jpeg), orientation: 6);
        Assert.True(Contains(jpeg, "Exif"u8));

        var result = _processor.Process(new MemoryStream(jpeg), "image/jpeg").Content;

        Assert.False(Contains(result, "Exif"u8));
        using var upright = SKBitmap.Decode(result);
        Assert.Equal((20, 40), (upright.Width, upright.Height));
        Assert.True(IsRed(upright.GetPixel(10, 5)), "The left half turns into the top half.");
        Assert.False(IsRed(upright.GetPixel(10, 35)));
    }

    [Fact]
    public void Large_images_are_capped_on_the_longer_side_and_keep_their_format()
    {
        var png = Encode(Halves(3000, 1000), SKEncodedImageFormat.Png);

        var result = _processor.Process(new MemoryStream(png), "image/png").Content;

        using var codec = SKCodec.Create(new MemoryStream(result));
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(SkiaImageProcessor.MaximumDimension, codec.Info.Width);
        Assert.Equal(853, codec.Info.Height);
    }

    [Fact]
    public void Content_that_is_not_an_image_is_refused()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            _processor.Process(new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0]), "image/png"));

        Assert.Equal("The image could not be read.", Assert.Single(exception.Errors["file"]));
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/gif", false)]
    [InlineData("application/pdf", false)]
    public void Only_jpeg_png_and_webp_are_rewritten(string contentType, bool rewritten) =>
        Assert.Equal(rewritten, _processor.Rewrites(contentType));

    private static SKBitmap Halves(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);
        using var red = new SKPaint { Color = SKColors.Red };
        canvas.DrawRect(0, 0, width / 2f, height, red);
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, 95))
        {
            return data.ToArray();
        }
    }

    /// <summary>Inserts an APP1 EXIF segment (orientation plus a GPS latitude reference) after the JPEG's SOI marker.</summary>
    private static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        using var tiff = new MemoryStream();
        using (var writer = new BinaryWriter(tiff))
        {
            writer.Write("II"u8);
            writer.Write((ushort)42);
            writer.Write(8u);
            writer.Write((ushort)2);
            writer.Write((ushort)0x0112); writer.Write((ushort)3); writer.Write(1u); writer.Write(orientation); writer.Write((ushort)0);
            writer.Write((ushort)0x8825); writer.Write((ushort)4); writer.Write(1u); writer.Write(38u);
            writer.Write(0u);
            writer.Write((ushort)1);
            writer.Write((ushort)0x0001); writer.Write((ushort)2); writer.Write(2u); writer.Write("N\0\0\0"u8);
            writer.Write(0u);
        }

        byte[] payload = [.. "Exif\0\0"u8, .. tiff.ToArray()];
        var length = payload.Length + 2;
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, .. jpeg[2..]];
    }

    private static bool Contains(byte[] data, ReadOnlySpan<byte> value) => data.AsSpan().IndexOf(value) >= 0;

    private static bool IsRed(SKColor color) => color.Red > 200 && color.Blue < 60;
}
