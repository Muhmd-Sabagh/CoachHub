using CoachHub.Domain.Assessments;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Assessments;

public interface IFormRepository
{
    Task<PagedResult<FormSummary>> ListDefinitionsAsync(FormAdminQuery query, CancellationToken cancellationToken);
    Task<PagedResult<AssessmentSubmissionSummary>> ListSubmissionsAsync(AssessmentSubmissionQuery query, CancellationToken cancellationToken);
    Task<AssessmentSubmissionDetail?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken);
    Task AddFormAsync(FormDefinition definition, FormVersion draft, CancellationToken cancellationToken);
    Task<FormDefinition?> FindDefinitionAsync(Guid id, CancellationToken cancellationToken);
    Task<FormGraph?> FindDraftAsync(Guid definitionId, CancellationToken cancellationToken);
    Task<FormGraph?> FindLatestPublishedAsync(Guid definitionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FormDefinition>> ListPublishedDefinitionsAsync(CancellationToken cancellationToken);
    Task AddSectionAsync(FormSection section, CancellationToken cancellationToken);
    Task AddQuestionAsync(FormQuestion question, IReadOnlyList<QuestionOption> options, CancellationToken cancellationToken);
    Task UpdateQuestionAsync(FormQuestion question, IReadOnlyList<QuestionOption> options, CancellationToken cancellationToken);
    Task DeleteQuestionAsync(FormQuestion question, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task AddDraftGraphAsync(FormVersion version, IReadOnlyList<FormSection> sections,
        IReadOnlyList<FormQuestion> questions, IReadOnlyList<QuestionOption> options,
        CancellationToken cancellationToken);
    Task<Client?> FindClientByCodesAsync(string clientCode, string formCode, CancellationToken cancellationToken);
    Task<bool> HasInitialSubmissionAsync(Guid clientId, CancellationToken cancellationToken);
    Task SubmitAsync(FormSubmission submission, IReadOnlyList<FormAnswer> answers, CancellationToken cancellationToken);
}