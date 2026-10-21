using CoachHub.Domain.Common;

namespace CoachHub.Domain.ClientFiles;

/// <summary>
/// A file the coach shared with a client (§43): a PDF, image, video or document stored as the client's media, which
/// the client opens through the ownership-checked media endpoint.
/// </summary>
public sealed class ClientFile : Entity, ITenantOwned
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 1000;

    private ClientFile() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid MediaId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? SharedByUserId { get; private set; }
    public DateTimeOffset SharedAt { get; private set; }

    public static ClientFile Share(Guid coachId, Guid clientId, Guid mediaId, string title, string? description, Guid? sharedByUserId, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (mediaId == Guid.Empty) throw new ArgumentException("A file is required.", nameof(mediaId));
        if (sharedByUserId == Guid.Empty) throw new ArgumentException("The sharer cannot be empty.", nameof(sharedByUserId));
        var file = new ClientFile
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = clientId,
            MediaId = mediaId,
            SharedByUserId = sharedByUserId,
            SharedAt = now
        };
        file.Describe(title, description);
        return file;
    }

    public void Describe(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required.", nameof(title));
        var name = title.Trim();
        if (name.Length > MaximumTitleLength) throw new ArgumentException($"A title has at most {MaximumTitleLength} characters.", nameof(title));
        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text is { Length: > MaximumDescriptionLength })
            throw new ArgumentException($"A description has at most {MaximumDescriptionLength} characters.", nameof(description));
        Title = name;
        Description = text;
    }
}
