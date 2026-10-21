using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Media;

namespace CoachHub.Application.Tests.Media;

public sealed class MediaServiceTests
{
    [Fact]
    public async Task Upload_stores_content_and_persists_safe_metadata()
    {
        var storage = new StubStorage();
        var repository = new StubRepository();
        var service = new MediaService(storage, repository);
        await using var content = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var result = await service.UploadAsync(
            content,
            @"..\portrait.png",
            "image/png",
            content.Length,
            MediaOwner.Platform(null),
            CancellationToken.None);

        Assert.Equal("portrait.png", result.OriginalFileName);
        Assert.Equal("generated.png", repository.Added!.StorageKey);
        Assert.Equal("image/png", storage.ReceivedContentType);
    }

    [Fact]
    public async Task Legacy_gif_image_is_supported_for_exercise_migration()
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream("GIF89a"u8.ToArray());

        await service.UploadAsync(
            content,
            "exercise.gif",
            "image/gif",
            content.Length,
            MediaOwner.Platform(null),
            CancellationToken.None);

        Assert.True(storage.StoreCalled);
        Assert.Equal("image/gif", storage.ReceivedContentType);
    }
    [Fact]
    public async Task Unsupported_content_type_is_rejected_before_storage()
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream([1]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                content,
                "payload.exe",
                "application/octet-stream",
                content.Length,
                MediaOwner.Platform(null),
                CancellationToken.None));

        Assert.Contains("contentType", exception.Errors);
        Assert.False(storage.StoreCalled);
    }

    [Fact]
    public async Task Declared_content_type_must_match_the_file_signature()
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream("not-a-real-png"u8.ToArray());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                content,
                "renamed.png",
                "image/png",
                content.Length,
                MediaOwner.Platform(null),
                CancellationToken.None));

        Assert.Contains("file", exception.Errors);
        Assert.False(storage.StoreCalled);
    }

    [Fact]
    public async Task Oversized_content_is_rejected_without_allocating_the_declared_size()
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream("%PDF-1.7"u8.ToArray());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAsync(
                content,
                "large.pdf",
                "application/pdf",
                MediaService.MaximumFileSizeBytes + 1,
                MediaOwner.Platform(null),
                CancellationToken.None));

        Assert.Contains("file", exception.Errors);
        Assert.False(storage.StoreCalled);
    }

    [Fact]
    public async Task Upload_records_the_owner_of_the_file()
    {
        var repository = new StubRepository();
        var service = new MediaService(new StubStorage(), repository);
        await using var content = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var owner = MediaOwner.OfClient(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await service.UploadAsync(content, "progress.png", "image/png", content.Length, owner, CancellationToken.None);

        Assert.Equal(owner.CoachId, repository.Added!.CoachId);
        Assert.Equal(owner.ClientId, repository.Added.ClientId);
        Assert.Equal(owner.UploadedByUserId, repository.Added.OwnerUserId);
    }

    [Fact]
    public async Task Delete_keeps_the_content_when_the_row_cannot_be_deleted()
    {
        var media = MediaAsset.Create("stored", "a.png", "image/png", 8, DateTimeOffset.UtcNow, null, null, null);
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository(media, refuseDelete: true));

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(media.Id, CancellationToken.None));

        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Delete_content_continues_past_storage_failures()
    {
        var storage = new StubStorage { FailOn = "first" };
        var service = new MediaService(storage, new StubRepository());

        await service.DeleteContentAsync(["first", "second"], CancellationToken.None);

        Assert.Equal(["second"], storage.Deleted);
    }

    [Fact]
    public void Only_platform_images_qualify_for_catalog_entries()
    {
        var coachId = Guid.NewGuid();
        Assert.True(MediaService.IsPlatformImage(
            MediaAsset.Create("a", "a.png", "image/png", 8, DateTimeOffset.UtcNow, null, null, null)));
        Assert.False(MediaService.IsPlatformImage(
            MediaAsset.Create("b", "b.pdf", "application/pdf", 8, DateTimeOffset.UtcNow, null, null, null)));
        Assert.False(MediaService.IsPlatformImage(
            MediaAsset.Create("c", "c.png", "image/png", 8, DateTimeOffset.UtcNow, coachId, Guid.NewGuid(), null)));
        Assert.False(MediaService.IsPlatformImage(null));
    }

    [Theory]
    [InlineData("video/mp4", new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D })]
    [InlineData("video/quicktime", new byte[] { 0, 0, 0, 0x14, 0x66, 0x74, 0x79, 0x70, 0x71, 0x74, 0x20, 0x20 })]
    [InlineData("video/webm", new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 1, 2 })]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { 0x50, 0x4B, 0x03, 0x04, 1 })]
    [InlineData("text/plain", new byte[] { 0x48, 0x69, 0x0A })]
    public async Task Shared_uploads_accept_videos_documents_and_text(string contentType, byte[] bytes)
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream(bytes);

        await service.UploadAsync(content, "file", contentType, content.Length, MediaOwner.Platform(null), MediaUploadPolicy.Shared, CancellationToken.None);

        Assert.True(storage.StoreCalled);
    }

    [Theory]
    [InlineData("video/mp4", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, "file")]
    [InlineData("text/plain", new byte[] { 0x48, 0x00, 0x69 }, "file")]
    [InlineData("application/zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 }, "contentType")]
    public async Task Shared_uploads_still_check_types_and_signatures(string contentType, byte[] bytes, string field)
    {
        var storage = new StubStorage();
        var service = new MediaService(storage, new StubRepository());
        await using var content = new MemoryStream(bytes);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(
            content, "file", contentType, content.Length, MediaOwner.Platform(null), MediaUploadPolicy.Shared, CancellationToken.None));

        Assert.Contains(field, exception.Errors);
        Assert.False(storage.StoreCalled);
    }

    [Fact]
    public async Task Size_limits_depend_on_the_upload_policy()
    {
        var service = new MediaService(new StubStorage(), new StubRepository());
        await using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]);

        var standard = await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(content, "big.pdf", "application/pdf",
            MediaService.MaximumFileSizeBytes + 1, MediaOwner.Platform(null), CancellationToken.None));
        Assert.Contains("20 MB", Assert.Single(standard.Errors["file"]));
        await service.UploadAsync(content, "big.pdf", "application/pdf", MediaService.MaximumFileSizeBytes + 1,
            MediaOwner.Platform(null), MediaUploadPolicy.Shared, CancellationToken.None);
        var shared = await Assert.ThrowsAsync<ValidationException>(() => service.UploadAsync(content, "big.pdf", "application/pdf",
            MediaService.MaximumSharedFileSizeBytes + 1, MediaOwner.Platform(null), MediaUploadPolicy.Shared, CancellationToken.None));
        Assert.Contains("50 MB", Assert.Single(shared.Errors["file"]));
    }

    private sealed class StubStorage : IMediaStorage
    {
        public List<string> Deleted { get; } = [];
        public string? FailOn { get; init; }
        public bool StoreCalled { get; private set; }
        public string? ReceivedContentType { get; private set; }

        public Task<StoredMedia> StoreAsync(
            Stream content,
            string originalFileName,
            string contentType,
            CancellationToken cancellationToken)
        {
            StoreCalled = true;
            ReceivedContentType = contentType;
            return Task.FromResult(new StoredMedia("generated.png"));
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            if (storageKey == FailOn) throw new IOException("Storage unavailable.");
            Deleted.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class StubRepository(MediaAsset? existing = null, bool refuseDelete = false) : IMediaRepository
    {
        public MediaAsset? Added { get; private set; }

        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken)
        {
            Added = media;
            return Task.CompletedTask;
        }

        public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(existing?.Id == id ? existing : null);

        public Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken) =>
            refuseDelete ? throw new ConflictException("Referenced.") : Task.CompletedTask;
    }
}