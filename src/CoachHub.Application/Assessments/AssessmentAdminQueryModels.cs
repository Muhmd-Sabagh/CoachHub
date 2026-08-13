using CoachHub.Application.Common.Models;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments;

public sealed record FormAdminQuery(int PageNumber = 1, int PageSize = 20, string? SearchTerm = null, AssessmentFormType? FormType = null, bool? IsArchived = null)
{
    public FormAdminQuery Normalize() => this with { PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 100), SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim() };
}
public sealed record AssessmentSubmissionQuery(int PageNumber = 1, int PageSize = 20, string? SearchTerm = null, Guid? ClientId = null, AssessmentFormType? FormType = null)
{
    public AssessmentSubmissionQuery Normalize() => this with { PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 100), SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim() };
}
public sealed record AssessmentSubmissionSummary(Guid Id, Guid ClientId, string ClientName, string ClientCode, Guid FormDefinitionId, string FormName, AssessmentFormType FormType, SubmissionSource Source, DateTimeOffset SubmittedAt, int AnswerCount);
public sealed record AssessmentAnswerResponse(Guid Id, Guid QuestionStableKey, string QuestionText, QuestionType QuestionType, string ValueJson, Guid? MediaId, string? ExternalMediaUrl);
public sealed record AssessmentSubmissionDetail(AssessmentSubmissionSummary Submission, IReadOnlyList<AssessmentAnswerResponse> Answers);

public sealed class AssessmentAdminQueryService(IFormRepository repository)
{
    public Task<PagedResult<FormSummary>> ListFormsAsync(FormAdminQuery query, CancellationToken token) => repository.ListDefinitionsAsync(query.Normalize(), token);
    public Task<PagedResult<AssessmentSubmissionSummary>> ListSubmissionsAsync(AssessmentSubmissionQuery query, CancellationToken token) => repository.ListSubmissionsAsync(query.Normalize(), token);
    public async Task<AssessmentSubmissionDetail> GetSubmissionAsync(Guid id, CancellationToken token) => await repository.GetSubmissionAsync(id, token) ?? throw new Common.Exceptions.NotFoundException("Assessment submission", id);
}