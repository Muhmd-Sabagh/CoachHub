using CoachHub.Domain.Media;

namespace CoachHub.Domain.Tests.Media;

public sealed class MediaAssetTests
{
    [Fact]
    public void Create_keeps_only_the_original_file_name()
    {
        var media = MediaAsset.Create(
            "stored-asset-key",
            @"..\uploads\portrait.png",
            "image/png",
            42,
            DateTimeOffset.UtcNow);

        Assert.Equal("portrait.png", media.OriginalFileName);
        Assert.Equal("stored-asset-key", media.StorageKey);
    }
}