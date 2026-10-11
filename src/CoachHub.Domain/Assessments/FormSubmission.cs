using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormSubmission : Entity, ITenantOwned
{
    private FormSubmission() { }
    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid FormDefinitionId { get; private set; }
    public Guid FormVersionId { get; private set; }
    public AssessmentFormType FormType { get; private set; }
    public Guid? InitialClientId { get; private set; }
    public SubmissionSource Source { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public string? ImportFingerprint { get; private set; }
    public string? ExternalSubmissionId { get; private set; }

    public static FormSubmission Create(
        Guid coachId, Guid clientId, Guid definitionId, Guid versionId, AssessmentFormType formType,
        SubmissionSource source, DateTimeOffset submittedAt,
        string? importFingerprint = null, string? externalSubmissionId = null)
    {
        if (clientId == Guid.Empty || definitionId == Guid.Empty || versionId == Guid.Empty)
            throw new ArgumentException("Client, definition, and version are required.");
        return new FormSubmission
        {
            CoachId = TenantKey.Required(coachId), ClientId = clientId, FormDefinitionId = definitionId, FormVersionId = versionId,
            FormType = formType,
            InitialClientId = formType == AssessmentFormType.InitialAssessment ? clientId : null,
            Source = source, SubmittedAt = submittedAt,
            ImportFingerprint = Optional(importFingerprint, 64, nameof(importFingerprint)),
            ExternalSubmissionId = Optional(externalSubmissionId, 500, nameof(externalSubmissionId))
        };
    }

    private static string? Optional(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ArgumentOutOfRangeException(parameter);
        return normalized;
    }
}