using CoachHub.Application.Media;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Media;

public sealed class FileSystemMediaStorage(IOptions<MediaStorageOptions> options) : IMediaStorage
{
    private readonly string _storageRoot = ResolveRoot(options.Value.StorageRoot);

    public async Task<StoredMedia> StoreAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storageRoot);

        var extension = ContentTypeExtension(contentType);
        var storageKey = Guid.NewGuid().ToString("N") + extension;
        var path = ResolveStoragePath(storageKey);

        await using var destination = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(destination, cancellationToken);
        return new StoredMedia(storageKey);
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveStoragePath(storageKey);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Stored media content was not found.", storageKey);
        }

        Stream content = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(content);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveStoragePath(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (Path.GetFileName(storageKey) != storageKey)
        {
            throw new InvalidOperationException("Invalid media storage key.");
        }

        var path = Path.GetFullPath(Path.Combine(_storageRoot, storageKey));
        var rootWithSeparator = _storageRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Media storage key escapes the configured root.");
        }

        return path;
    }

    private static string ResolveRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("Media StorageRoot is required for FileSystem storage.");
        }

        return Path.GetFullPath(configuredRoot);
    }

    private static string ContentTypeExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "application/pdf" => ".pdf",
        _ => throw new InvalidOperationException("Unsupported media content type.")
    };
}
