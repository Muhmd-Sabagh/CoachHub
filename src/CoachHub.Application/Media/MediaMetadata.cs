namespace CoachHub.Application.Media;

public sealed record MediaMetadata(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);
