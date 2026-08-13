using CoachHub.Application.Assessments;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoachHub.Infrastructure.Assessments;

public sealed class FormRepository(CoachHubDbContext dbContext) : IFormRepository
{
    public async Task<CoachHub.Application.Common.Models.PagedResult<FormSummary>> ListDefinitionsAsync(FormAdminQuery query, CancellationToken token)
    {
        var source = dbContext.Set<FormDefinition>().AsNoTracking();
        if (query.SearchTerm is not null) source = source.Where(x => x.Name.Contains(query.SearchTerm));
        if (query.FormType.HasValue) source = source.Where(x => x.FormType == query.FormType);
        if (query.IsArchived.HasValue) source = source.Where(x => x.IsArchived == query.IsArchived);
        var total = await source.LongCountAsync(token);
        var items = await source.OrderBy(x => x.Name).Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(x => new FormSummary(x.Id, x.Name, x.FormType, x.IsArchived)).ToArrayAsync(token);
        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<CoachHub.Application.Common.Models.PagedResult<AssessmentSubmissionSummary>> ListSubmissionsAsync(AssessmentSubmissionQuery query, CancellationToken token)
    {
        var source = from submission in dbContext.Set<FormSubmission>().AsNoTracking() join client in dbContext.Set<Client>().AsNoTracking() on submission.ClientId equals client.Id join definition in dbContext.Set<FormDefinition>().AsNoTracking() on submission.FormDefinitionId equals definition.Id select new { submission, client, definition };
        if (query.ClientId.HasValue) source = source.Where(x => x.submission.ClientId == query.ClientId);
        if (query.FormType.HasValue) source = source.Where(x => x.submission.FormType == query.FormType);
        if (query.SearchTerm is not null) source = source.Where(x => x.client.Name.Contains(query.SearchTerm) || x.client.ClientCode.Contains(query.SearchTerm) || x.definition.Name.Contains(query.SearchTerm));
        var total = await source.LongCountAsync(token);
        var items = await source.OrderByDescending(x => x.submission.SubmittedAt).Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(x => new AssessmentSubmissionSummary(x.submission.Id, x.client.Id, x.client.Name, x.client.ClientCode, x.definition.Id, x.definition.Name, x.submission.FormType, x.submission.Source, x.submission.SubmittedAt, dbContext.Set<FormAnswer>().Count(a => a.FormSubmissionId == x.submission.Id))).ToArrayAsync(token);
        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<AssessmentSubmissionDetail?> GetSubmissionAsync(Guid id, CancellationToken token)
    {
        var summary = await (from submission in dbContext.Set<FormSubmission>().AsNoTracking() join client in dbContext.Set<Client>().AsNoTracking() on submission.ClientId equals client.Id join definition in dbContext.Set<FormDefinition>().AsNoTracking() on submission.FormDefinitionId equals definition.Id where submission.Id == id select new AssessmentSubmissionSummary(submission.Id, client.Id, client.Name, client.ClientCode, definition.Id, definition.Name, submission.FormType, submission.Source, submission.SubmittedAt, dbContext.Set<FormAnswer>().Count(a => a.FormSubmissionId == submission.Id))).SingleOrDefaultAsync(token);
        if (summary is null) return null;
        var answers = await dbContext.Set<FormAnswer>().AsNoTracking().Where(x => x.FormSubmissionId == id).OrderBy(x => x.QuestionTextSnapshot).Select(x => new AssessmentAnswerResponse(x.Id, x.QuestionStableKey, x.QuestionTextSnapshot, x.QuestionTypeSnapshot, x.ValueJson, x.MediaId, x.ExternalMediaUrl)).ToArrayAsync(token);
        return new(summary, answers);
    }
    public async Task AddFormAsync(FormDefinition definition, FormVersion draft, CancellationToken token)
    {
        dbContext.Add(definition); dbContext.Add(draft); await dbContext.SaveChangesAsync(token);
    }
    public Task<FormDefinition?> FindDefinitionAsync(Guid id, CancellationToken token) =>
        dbContext.Set<FormDefinition>().SingleOrDefaultAsync(item => item.Id == id, token);
    public async Task<FormGraph?> FindDraftAsync(Guid definitionId, CancellationToken token)
    {
        var version = await dbContext.Set<FormVersion>().SingleOrDefaultAsync(
            item => item.FormDefinitionId == definitionId && item.Status == FormVersionStatus.Draft, token);
        return version is null ? null : await GraphAsync(version, token);
    }
    public async Task<FormGraph?> FindLatestPublishedAsync(Guid definitionId, CancellationToken token)
    {
        var version = await dbContext.Set<FormVersion>().AsNoTracking()
            .Where(item => item.FormDefinitionId == definitionId && item.Status == FormVersionStatus.Published)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(token);
        return version is null ? null : await GraphAsync(version, token, true);
    }
    public async Task<IReadOnlyList<FormDefinition>> ListPublishedDefinitionsAsync(CancellationToken token) =>
        await dbContext.Set<FormDefinition>().AsNoTracking()
            .Where(definition => dbContext.Set<FormVersion>().Any(version =>
                version.FormDefinitionId == definition.Id && version.Status == FormVersionStatus.Published))
            .OrderBy(definition => definition.Name).ToArrayAsync(token);
    public async Task AddSectionAsync(FormSection section, CancellationToken token)
    { dbContext.Add(section); await dbContext.SaveChangesAsync(token); }
    public async Task AddQuestionAsync(FormQuestion question, IReadOnlyList<QuestionOption> options, CancellationToken token)
    { dbContext.Add(question); dbContext.AddRange(options); await dbContext.SaveChangesAsync(token); }
    public async Task UpdateQuestionAsync(
        FormQuestion question,
        IReadOnlyList<QuestionOption> options,
        CancellationToken token)
    {
        var existing = await dbContext.Set<QuestionOption>()
            .Where(option => option.FormQuestionId == question.Id)
            .ToArrayAsync(token);
        dbContext.RemoveRange(existing);
        dbContext.AddRange(options);
        await dbContext.SaveChangesAsync(token);
    }
    public async Task DeleteQuestionAsync(FormQuestion question, CancellationToken token)
    { dbContext.Remove(question); await dbContext.SaveChangesAsync(token); }
    public Task SaveChangesAsync(CancellationToken token) => dbContext.SaveChangesAsync(token);
    public async Task AddDraftGraphAsync(FormVersion version, IReadOnlyList<FormSection> sections,
        IReadOnlyList<FormQuestion> questions, IReadOnlyList<QuestionOption> options, CancellationToken token)
    {
        dbContext.Add(version); dbContext.AddRange(sections); dbContext.AddRange(questions);
        dbContext.AddRange(options); await dbContext.SaveChangesAsync(token);
    }
    public Task<Client?> FindClientByCodesAsync(string clientCode, string formCode, CancellationToken token) =>
        dbContext.Set<Client>().SingleOrDefaultAsync(client =>
            client.ClientCode == clientCode && client.FormCode == formCode, token);
    public Task<bool> HasInitialSubmissionAsync(Guid clientId, CancellationToken token) =>
        dbContext.Set<FormSubmission>().AnyAsync(submission => submission.InitialClientId == clientId, token);

    public async Task SubmitAsync(FormSubmission submission, IReadOnlyList<FormAnswer> answers, CancellationToken token)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
            transaction = await dbContext.Database.BeginTransactionAsync(token);
        try
        {
            dbContext.Add(submission); dbContext.AddRange(answers);
            await dbContext.SaveChangesAsync(token);
            if (transaction is not null) await transaction.CommitAsync(token);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            throw new ConflictException(
                "The submission conflicts with an existing assessment, including the one-initial rule.");
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<FormGraph> GraphAsync(FormVersion version, CancellationToken token, bool noTracking = false)
    {
        var definitions = dbContext.Set<FormDefinition>().AsQueryable();
        var sections = dbContext.Set<FormSection>().AsQueryable();
        var questions = dbContext.Set<FormQuestion>().AsQueryable();
        var options = dbContext.Set<QuestionOption>().AsQueryable();
        if (noTracking)
        {
            definitions = definitions.AsNoTracking(); sections = sections.AsNoTracking();
            questions = questions.AsNoTracking(); options = options.AsNoTracking();
        }
        var definition = await definitions.SingleAsync(item => item.Id == version.FormDefinitionId, token);
        var sectionItems = await sections.Where(item => item.FormVersionId == version.Id).ToArrayAsync(token);
        var questionItems = await questions.Where(item => item.FormVersionId == version.Id).ToArrayAsync(token);
        var ids = questionItems.Select(item => item.Id).ToArray();
        var optionItems = await options.Where(item => ids.Contains(item.FormQuestionId)).ToArrayAsync(token);
        return new(definition, version, sectionItems, questionItems, optionItems);
    }
}