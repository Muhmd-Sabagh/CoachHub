using CoachHub.Domain.Common;

namespace CoachHub.Domain.Media;

/// <summary>
/// A stored file. Ownership decides who may open it (§52, §62):
/// <list type="bullet">
/// <item>no coach: platform media (catalog images), visible to every signed-in user;</item>
/// <item>a coach and no client: the coach's library (their avatar, later branding), visible to the coach and their clients;</item>
/// <item>a coach and a client: the client's media, visible to that client and their coach.</item>
/// </list>
/// Administrators see everything. Client media follows the client when they are transferred.
/// </summary>
public sealed class MediaAsset : Entity, IOptionalTenantOwned
{
    private MediaAsset()
    {
    }

    private MediaAsset(
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt,
        Guid? coachId,
        Guid? clientId,
        Guid? ownerUserId)
    {
        StorageKey = storageKey;
        CoachId = coachId;
        ClientId = clientId;
        OwnerUserId = ownerUserId;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAt = createdAt;
    }

    public string StorageKey { get; private set; } = string.Empty;

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The owning coach; null for platform media.</summary>
    public Guid? CoachId { get; private set; }

    /// <summary>The client the file belongs to, if any. Requires a coach.</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>The account that uploaded the file; null for anonymous legacy form uploads.</summary>
    public Guid? OwnerUserId { get; private set; }

    public bool IsPlatformMedia => CoachId is null;

    public static MediaAsset Create(
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        DateTimeOffset createdAt,
        Guid? coachId,
        Guid? clientId,
        Guid? ownerUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier cannot be empty.", nameof(clientId));
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner identifier cannot be empty.", nameof(ownerUserId));
        if (clientId.HasValue && coachId is null)
            throw new ArgumentException("Client media must belong to the client's coach.", nameof(coachId));

        return new MediaAsset(
            storageKey,
            FileNameOnly(originalFileName),
            contentType,
            sizeBytes,
            createdAt,
            TenantKey.Optional(coachId),
            clientId,
            ownerUserId);
    }

    // Browsers on Windows may send "C:\dir\name.png"; Path.GetFileName ignores '\' on Linux,
    // so strip both separators regardless of the host OS.
    private static string FileNameOnly(string originalFileName)
    {
        var name = originalFileName[(originalFileName.LastIndexOfAny(['/', '\\']) + 1)..];
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(originalFileName));
        return name;
    }
}
