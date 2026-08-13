using CoachHub.Infrastructure.Media;
using Microsoft.Extensions.Options;

namespace CoachHub.IntegrationTests.Media;

public sealed class FileSystemMediaStorageTests : IDisposable
{
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "coachhub-media-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Store_open_and_delete_round_trip_content()
    {
        var storage = new FileSystemMediaStorage(Options.Create(new MediaStorageOptions
        {
            Provider = "FileSystem",
            StorageRoot = _storageRoot
        }));
        await using var input = new MemoryStream([10, 20, 30]);

        var stored = await storage.StoreAsync(
            input,
            "untrusted.exe",
            "image/png",
            CancellationToken.None);

        Assert.EndsWith(".png", stored.StorageKey);
        Assert.DoesNotContain("untrusted", stored.StorageKey);

        byte[] actual;
        await using (var output = await storage.OpenReadAsync(
            stored.StorageKey,
            CancellationToken.None))
        {
            using var copy = new MemoryStream();
            await output.CopyToAsync(copy);
            actual = copy.ToArray();
        }

        Assert.Equal([10, 20, 30], actual);
        await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            storage.OpenReadAsync(stored.StorageKey, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }
}