using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormImportColumnMapping : Entity
{
    private FormImportColumnMapping() { }
    public Guid FormImportProfileId { get; private set; }
    public string ExternalColumnKey { get; private set; } = string.Empty;
    public string Header { get; private set; } = string.Empty;
    public Guid QuestionStableKey { get; private set; }

    public static FormImportColumnMapping Create(
        Guid profileId, string externalColumnKey, string header, Guid questionStableKey)
    {
        if (profileId == Guid.Empty || questionStableKey == Guid.Empty)
            throw new ArgumentException("Profile and question stable key are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(externalColumnKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        externalColumnKey = externalColumnKey.Trim();
        header = header.Trim();
        if (externalColumnKey.Length > 100 || header.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(header));
        return new FormImportColumnMapping
        {
            FormImportProfileId = profileId,
            ExternalColumnKey = externalColumnKey,
            Header = header,
            QuestionStableKey = questionStableKey
        };
    }
}