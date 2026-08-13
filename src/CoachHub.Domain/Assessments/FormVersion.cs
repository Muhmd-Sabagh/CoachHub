using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormVersion : Entity
{
    private FormVersion() { }
    public Guid FormDefinitionId { get; private set; }
    public int VersionNumber { get; private set; }
    public FormVersionStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static FormVersion CreateDraft(Guid definitionId, int versionNumber)
    {
        if (definitionId == Guid.Empty) throw new ArgumentException("Form is required.", nameof(definitionId));
        if (versionNumber <= 0) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        return new FormVersion
        {
            FormDefinitionId = definitionId,
            VersionNumber = versionNumber,
            Status = FormVersionStatus.Draft
        };
    }
    public void EnsureDraft()
    {
        if (Status != FormVersionStatus.Draft)
            throw new InvalidOperationException("Published form versions are immutable.");
    }
    public void Publish(DateTimeOffset publishedAt)
    {
        EnsureDraft();
        Status = FormVersionStatus.Published;
        PublishedAt = publishedAt;
    }
}