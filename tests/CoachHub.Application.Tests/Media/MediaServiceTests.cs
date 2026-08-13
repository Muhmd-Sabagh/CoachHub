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
                CancellationToken.None));

        Assert.Contains("file", exception.Errors);
        Assert.False(storage.StoreCalled);
    }

    private sealed class StubStorage : IMediaStorage
    {
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

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StubRepository : IMediaRepository
    {
        public MediaAsset? Added { get; private set; }

        public Task AddAsync(MediaAsset media, CancellationToken cancellationToken)
        {
            Added = media;
            return Task.CompletedTask;
        }

        public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<MediaAsset?>(null);

        public Task DeleteAsync(MediaAsset media, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}