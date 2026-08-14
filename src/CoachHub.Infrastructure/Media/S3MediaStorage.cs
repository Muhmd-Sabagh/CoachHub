using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CoachHub.Application.Media;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Media;

public sealed class S3MediaStorage : IMediaStorage, IDisposable
{
    private readonly MediaStorageOptions _options;
    private readonly IAmazonS3 _client;

    public S3MediaStorage(IOptions<MediaStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.BucketName)) throw new InvalidOperationException("Media BucketName is required.");
        if (string.IsNullOrWhiteSpace(_options.AccessKey) || string.IsNullOrWhiteSpace(_options.SecretKey)) throw new InvalidOperationException("S3-compatible media credentials are required.");
        var config = new AmazonS3Config { ForcePathStyle = _options.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl)) config.ServiceURL = _options.ServiceUrl;
        else config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.SecretKey), config);
    }

    public async Task<StoredMedia> StoreAsync(Stream content, string originalFileName, string contentType, CancellationToken token)
    {
        var key = $"{_options.KeyPrefix.Trim('/')}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{Extension(contentType)}".TrimStart('/');
        await _client.PutObjectAsync(new PutObjectRequest { BucketName = _options.BucketName, Key = key, InputStream = content, ContentType = contentType, AutoCloseStream = false }, token);
        return new StoredMedia(key);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken token)
    {
        ValidateKey(storageKey);
        using var response = await _client.GetObjectAsync(_options.BucketName, storageKey, token);
        var copy = new MemoryStream(); await response.ResponseStream.CopyToAsync(copy, token); copy.Position = 0; return copy;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken token)
    {
        ValidateKey(storageKey); await _client.DeleteObjectAsync(_options.BucketName, storageKey, token);
    }

    public void Dispose() => _client.Dispose();
    private static void ValidateKey(string key) { if (string.IsNullOrWhiteSpace(key) || key.StartsWith('/') || key.Contains("..", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid media storage key."); }
    private static string Extension(string type) => type switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif", "application/pdf" => ".pdf", _ => throw new InvalidOperationException("Unsupported media content type.") };
}
