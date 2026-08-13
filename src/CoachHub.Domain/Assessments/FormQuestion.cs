using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormQuestion : Entity
{
    private FormQuestion() { }
    public Guid FormVersionId { get; private set; }
    public Guid? FormSectionId { get; private set; }
    public Guid StableKey { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public QuestionType QuestionType { get; private set; }
    public bool IsRequired { get; private set; }
    public int Order { get; private set; }

    public static FormQuestion Create(
        Guid versionId, Guid? sectionId, Guid stableKey, string text,
        QuestionType type, bool required, int order)
    {
        if (versionId == Guid.Empty) throw new ArgumentException("Version is required.", nameof(versionId));
        if (stableKey == Guid.Empty) throw new ArgumentException("Stable key is required.", nameof(stableKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();
        if (text.Length > 1000) throw new ArgumentOutOfRangeException(nameof(text));
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        return new FormQuestion
        {
            FormVersionId = versionId, FormSectionId = sectionId, StableKey = stableKey,
            Text = text, QuestionType = type, IsRequired = required, Order = order
        };
    }
    public void Update(
        Guid? sectionId,
        string text,
        QuestionType type,
        bool required,
        int order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();
        if (text.Length > 1000) throw new ArgumentOutOfRangeException(nameof(text));
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        FormSectionId = sectionId;
        Text = text;
        QuestionType = type;
        IsRequired = required;
        Order = order;
    }
    public void Reorder(int order)
    {
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        Order = order;
    }
}