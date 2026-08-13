using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormSection : Entity
{
    private FormSection() { }
    public Guid FormVersionId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public static FormSection Create(Guid versionId, string title, int order)
    {
        if (versionId == Guid.Empty) throw new ArgumentException("Version is required.", nameof(versionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
        title = title.Trim();
        if (title.Length > 200) throw new ArgumentOutOfRangeException(nameof(title));
        return new FormSection { FormVersionId = versionId, Title = title, Order = order };
    }
}