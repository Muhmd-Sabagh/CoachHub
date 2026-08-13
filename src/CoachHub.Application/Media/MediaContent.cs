namespace CoachHub.Application.Media;

public sealed record MediaContent(
    Stream Content,
    string ContentType,
    string FileName,
    long SizeBytes);
