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

    [Theory]
    [InlineData(@"C:\Users\client\portrait.png")]
    [InlineData("../uploads/portrait.png")]
    [InlineData(@"..\mixed/dir\portrait.png")]
    public void Create_strips_both_path_separators_on_every_os(string originalFileName)
    {
        var media = MediaAsset.Create("key", originalFileName, "image/png", 42, DateTimeOffset.UtcNow);

        Assert.Equal("portrait.png", media.OriginalFileName);
    }

    [Theory]
    [InlineData("uploads/")]
    [InlineData(@"uploads\")]
    public void Create_rejects_a_path_without_a_file_name(string originalFileName)
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", originalFileName, "image/png", 42, DateTimeOffset.UtcNow));
    }
}