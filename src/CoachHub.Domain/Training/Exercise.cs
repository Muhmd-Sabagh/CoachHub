using CoachHub.Domain.Common;

namespace CoachHub.Domain.Training;

public sealed class Exercise : Entity
{
    private Exercise() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid ExerciseCategoryId { get; private set; }
    public Guid? MediaId { get; private set; }
    public string? YouTubeUrl { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Exercise Create(
        string nameEn,
        string? nameAr,
        Guid exerciseCategoryId,
        Guid? mediaId,
        string? youTubeUrl,
        bool isActive = true)
    {
        var exercise = new Exercise();
        exercise.Update(nameEn, nameAr, exerciseCategoryId, mediaId, youTubeUrl, isActive);
        return exercise;
    }

    public void Update(
        string nameEn,
        string? nameAr,
        Guid exerciseCategoryId,
        Guid? mediaId,
        string? youTubeUrl,
        bool isActive)
    {
        NameEn = Required(nameEn, 255, nameof(nameEn));
        NameAr = Optional(nameAr, 255, nameof(nameAr));
        if (exerciseCategoryId == Guid.Empty)
        {
            throw new ArgumentException("An exercise category is required.", nameof(exerciseCategoryId));
        }
        ExerciseCategoryId = exerciseCategoryId;
        MediaId = mediaId;
        YouTubeUrl = NormalizeYouTubeUrl(youTubeUrl);
        IsActive = isActive;
    }

    public static bool IsValidYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost;
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > 500 || !IsValidYouTubeUrl(normalized))
        {
            throw new ArgumentException("A valid HTTPS YouTube URL is required.", nameof(value));
        }
        return normalized;
    }

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }
}