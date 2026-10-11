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
            DateTimeOffset.UtcNow,
            coachId: null,
            clientId: null,
            ownerUserId: null);

        Assert.Equal("portrait.png", media.OriginalFileName);
        Assert.Equal("stored-asset-key", media.StorageKey);
    }

    [Theory]
    [InlineData(@"C:\Users\client\portrait.png")]
    [InlineData("../uploads/portrait.png")]
    [InlineData(@"..\mixed/dir\portrait.png")]
    public void Create_strips_both_path_separators_on_every_os(string originalFileName)
    {
        var media = MediaAsset.Create("key", originalFileName, "image/png", 42, DateTimeOffset.UtcNow, null, null, null);

        Assert.Equal("portrait.png", media.OriginalFileName);
    }

    [Theory]
    [InlineData("uploads/")]
    [InlineData(@"uploads\")]
    public void Create_rejects_a_path_without_a_file_name(string originalFileName)
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", originalFileName, "image/png", 42, DateTimeOffset.UtcNow, null, null, null));
    }

    [Fact]
    public void Media_without_a_coach_is_platform_media()
    {
        var media = MediaAsset.Create("key", "food.png", "image/png", 42, DateTimeOffset.UtcNow, null, null, Guid.NewGuid());

        Assert.True(media.IsPlatformMedia);
        Assert.Null(media.ClientId);
    }

    [Fact]
    public void Client_media_records_coach_client_and_uploader()
    {
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var media = MediaAsset.Create("key", "photo.jpg", "image/jpeg", 42, DateTimeOffset.UtcNow, coachId, clientId, userId);

        Assert.False(media.IsPlatformMedia);
        Assert.Equal(coachId, media.CoachId);
        Assert.Equal(clientId, media.ClientId);
        Assert.Equal(userId, media.OwnerUserId);
    }

    [Fact]
    public void Client_media_requires_the_clients_coach()
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", "photo.jpg", "image/jpeg", 42, DateTimeOffset.UtcNow, null, Guid.NewGuid(), null));
    }

    [Fact]
    public void Empty_owner_identifiers_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", "a.png", "image/png", 42, DateTimeOffset.UtcNow, Guid.Empty, null, null));
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", "a.png", "image/png", 42, DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.Empty, null));
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.Create("key", "a.png", "image/png", 42, DateTimeOffset.UtcNow, null, null, Guid.Empty));
    }
}
