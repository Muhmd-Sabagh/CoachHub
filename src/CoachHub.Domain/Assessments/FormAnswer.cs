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

    public static FormAnswer Create(
        Guid submissionId, FormQuestion question, string valueJson, Guid? mediaId)
    {
        if (submissionId == Guid.Empty) throw new ArgumentException("Submission is required.", nameof(submissionId));
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);
        return new FormAnswer
        {
            FormSubmissionId = submissionId, FormQuestionId = question.Id,
            QuestionStableKey = question.StableKey, QuestionTextSnapshot = question.Text,
            QuestionTypeSnapshot = question.QuestionType, ValueJson = valueJson, MediaId = mediaId
        };
    }
}