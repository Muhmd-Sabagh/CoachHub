using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormAnswer : Entity
{
    private FormAnswer() { }
    public Guid FormSubmissionId { get; private set; }
    public Guid FormQuestionId { get; private set; }
    public Guid QuestionStableKey { get; private set; }
    public string QuestionTextSnapshot { get; private set; } = string.Empty;
    public QuestionType QuestionTypeSnapshot { get; private set; }
    public string ValueJson { get; private set; } = string.Empty;
    public Guid? MediaId { get; private set; }
    public string? ExternalMediaUrl { get; private set; }

    public static FormAnswer Create(
        Guid submissionId, FormQuestion question, string valueJson, Guid? mediaId,
        string? externalMediaUrl = null)
    {
        if (submissionId == Guid.Empty) throw new ArgumentException("Submission is required.", nameof(submissionId));
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);
        return new FormAnswer
        {
            FormSubmissionId = submissionId, FormQuestionId = question.Id,
            QuestionStableKey = question.StableKey, QuestionTextSnapshot = question.Text,
            QuestionTypeSnapshot = question.QuestionType, ValueJson = valueJson, MediaId = mediaId,
            ExternalMediaUrl = NormalizeUrl(externalMediaUrl)
        };
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 2000 || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !(uri.Host.Equals("google.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A valid Google HTTPS media URL is required.", nameof(value));
        return normalized;
    }
}