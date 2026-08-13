using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class QuestionOption : Entity
{
    private QuestionOption() { }
    public Guid FormQuestionId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public static QuestionOption Create(Guid questionId, string value, string label, int order)
    {
        if (questionId == Guid.Empty) throw new ArgumentException("Question is required.", nameof(questionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(value); ArgumentException.ThrowIfNullOrWhiteSpace(label);
        value = value.Trim(); label = label.Trim();
        if (value.Length > 100 || label.Length > 500 || order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        return new QuestionOption { FormQuestionId = questionId, Value = value, Label = label, Order = order };
    }
}