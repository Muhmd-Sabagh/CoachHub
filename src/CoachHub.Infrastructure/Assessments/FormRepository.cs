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

    // Tenant isolation: IgnoreQueryFilters lifts the filters of the WHOLE query, not just one join, so the
    // submission queries below stay fully filtered. A transferred client's submissions may reference the
    // previous coach's form (D7); those names are read in separate queries, only for definitions that
    // visible submissions reference.
    public async Task<CoachHub.Application.Common.Models.PagedResult<AssessmentSubmissionSummary>> ListSubmissionsAsync(AssessmentSubmissionQuery query, CancellationToken token)
    {
        var source = from submission in dbContext.Set<FormSubmission>().AsNoTracking() join client in dbContext.Set<Client>().AsNoTracking() on submission.ClientId equals client.Id select new { submission, client };
        if (query.ClientId.HasValue) source = source.Where(x => x.submission.ClientId == query.ClientId);
        if (query.FormType.HasValue) source = source.Where(x => x.submission.FormType == query.FormType);
        if (query.SearchTerm is not null)
        {
            var referenced = await source.Select(x => x.submission.FormDefinitionId).Distinct().ToArrayAsync(token);
            var matchingForms = await dbContext.Set<FormDefinition>().IgnoreQueryFilters().AsNoTracking()
                .Where(x => referenced.Contains(x.Id) && x.Name.Contains(query.SearchTerm)).Select(x => x.Id).ToArrayAsync(token);
            source = source.Where(x => x.client.Name.Contains(query.SearchTerm) || matchingForms.Contains(x.submission.FormDefinitionId));
        }
        var total = await source.LongCountAsync(token);
        var rows = await source.OrderByDescending(x => x.submission.SubmittedAt).Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.submission.Id, ClientId = x.client.Id, x.client.Name, x.submission.FormDefinitionId, x.submission.FormType, x.submission.Source, x.submission.SubmittedAt, Answers = dbContext.Set<FormAnswer>().Count(a => a.FormSubmissionId == x.submission.Id) })
            .ToArrayAsync(token);
        var names = await DefinitionNamesAsync(rows.Select(x => x.FormDefinitionId), token);
        var items = rows.Select(x => new AssessmentSubmissionSummary(x.Id, x.ClientId, x.Name, x.FormDefinitionId, names.GetValueOrDefault(x.FormDefinitionId, string.Empty), x.FormType, x.Source, x.SubmittedAt, x.Answers)).ToArray();
        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<AssessmentSubmissionDetail?> GetSubmissionAsync(Guid id, CancellationToken token)
    {
        var row = await (from submission in dbContext.Set<FormSubmission>().AsNoTracking() join client in dbContext.Set<Client>().AsNoTracking() on submission.ClientId equals client.Id where submission.Id == id
            select new { submission.Id, ClientId = client.Id, client.Name, submission.FormDefinitionId, submission.FormType, submission.Source, submission.SubmittedAt, Answers = dbContext.Set<FormAnswer>().Count(a => a.FormSubmissionId == submission.Id) }).SingleOrDefaultAsync(token);
        if (row is null) return null;
        var names = await DefinitionNamesAsync([row.FormDefinitionId], token);
        var summary = new AssessmentSubmissionSummary(row.Id, row.ClientId, row.Name, row.FormDefinitionId, names.GetValueOrDefault(row.FormDefinitionId, string.Empty), row.FormType, row.Source, row.SubmittedAt, row.Answers);
        var answers = await dbContext.Set<FormAnswer>().AsNoTracking().Where(x => x.FormSubmissionId == id).OrderBy(x => x.QuestionTextSnapshot).Select(x => new AssessmentAnswerResponse(x.Id, x.QuestionStableKey, x.QuestionTextSnapshot, x.QuestionTypeSnapshot, x.ValueJson, x.MediaId, x.ExternalMediaUrl)).ToArrayAsync(token);
        return new(summary, answers);
    }

    /// <summary>Names of forms referenced by submissions the caller can already see, whoever owns the form.</summary>
    private async Task<Dictionary<Guid, string>> DefinitionNamesAsync(IEnumerable<Guid> definitionIds, CancellationToken token)
    {
        var ids = definitionIds.Distinct().ToArray();
        return await dbContext.Set<FormDefinition>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, token);
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
            item => item.FormDefinitionId == definitionId && item.Status == FormVersionStatus.Draft &&
                dbContext.Set<FormDefinition>().Any(definition => definition.Id == definitionId), token);
        return version is null ? null : await GraphAsync(version, token);
    }
    public async Task<FormGraph?> FindLatestPublishedAsync(Guid definitionId, CancellationToken token)
    {
        var version = await dbContext.Set<FormVersion>().AsNoTracking()
            .Where(item => item.FormDefinitionId == definitionId && item.Status == FormVersionStatus.Published &&
                dbContext.Set<FormDefinition>().Any(definition => definition.Id == definitionId))
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(token);
        return version is null ? null : await GraphAsync(version, token, true);
    }
    public async Task<IReadOnlyList<FormDefinition>> ListPublishedDefinitionsAsync(Guid coachId, CancellationToken token) =>
        await dbContext.Set<FormDefinition>().AsNoTracking()
            .Where(definition => definition.CoachId == coachId)
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
    public Task<Client?> FindClientAsync(Guid clientId, CancellationToken token) =>
        dbContext.Set<Client>().SingleOrDefaultAsync(client => client.Id == clientId, token);
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