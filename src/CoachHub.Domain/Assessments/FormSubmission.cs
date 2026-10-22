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

    public static FormSubmission Create(
        Guid coachId, Guid clientId, Guid definitionId, Guid versionId, AssessmentFormType formType,
        SubmissionSource source, DateTimeOffset submittedAt)
    {
        if (clientId == Guid.Empty || definitionId == Guid.Empty || versionId == Guid.Empty)
            throw new ArgumentException("Client, definition, and version are required.");
        return new FormSubmission
        {
            CoachId = TenantKey.Required(coachId), ClientId = clientId, FormDefinitionId = definitionId, FormVersionId = versionId,
            FormType = formType,
            InitialClientId = formType == AssessmentFormType.InitialAssessment ? clientId : null,
            Source = source, SubmittedAt = submittedAt
        };
    }
}