using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments;

public sealed class FormAdminService(IFormRepository repository, TimeProvider timeProvider)
{
    public async Task<FormVersionResponse> CreateAsync(CreateFormInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw Validation("name", "A form name is required.");
        var definition = FormDefinition.Create(input.Name, input.FormType);
        var draft = FormVersion.CreateDraft(definition.Id, 1);
        await repository.AddFormAsync(definition, draft, cancellationToken);
        return Map(new(definition, draft, [], [], []));
    }

    public async Task<FormSummary> UpdateAsync(Guid id, string name, bool archived, CancellationToken cancellationToken)
    {
        var definition = await repository.FindDefinitionAsync(id, cancellationToken)
            ?? throw new NotFoundException("Form", id);
        definition.Update(name); definition.SetArchived(archived);
        await repository.SaveChangesAsync(cancellationToken);
        return new(definition.Id, definition.Name, definition.FormType, definition.IsArchived);
    }

    public async Task<SectionResponse> AddSectionAsync(Guid definitionId, SectionInput input, CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        graph.Version.EnsureDraft();
        var section = FormSection.Create(graph.Version.Id, input.Title, input.Order);
        await repository.AddSectionAsync(section, cancellationToken);
        return new(section.Id, section.Title, section.Order);
    }

    public async Task<QuestionResponse> AddQuestionAsync(Guid definitionId, QuestionInput input, CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        graph.Version.EnsureDraft();
        ValidateQuestion(input, graph);
        var question = FormQuestion.Create(
            graph.Version.Id, input.SectionId, Guid.NewGuid(), input.Text,
            input.QuestionType, input.IsRequired, input.Order);
        var options = input.Options.Select(option =>
            QuestionOption.Create(question.Id, option.Value, option.Label, option.Order)).ToArray();
        await repository.AddQuestionAsync(question, options, cancellationToken);
        return Map(question, options);
    }

    public async Task<QuestionResponse> UpdateQuestionAsync(
        Guid definitionId,
        Guid questionId,
        QuestionInput input,
        CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        graph.Version.EnsureDraft();
        ValidateQuestion(input, graph);
        var question = graph.Questions.SingleOrDefault(item => item.Id == questionId)
            ?? throw new NotFoundException("Question", questionId);
        question.Update(input.SectionId, input.Text, input.QuestionType, input.IsRequired, input.Order);
        var options = input.Options.Select(option =>
            QuestionOption.Create(question.Id, option.Value, option.Label, option.Order)).ToArray();
        await repository.UpdateQuestionAsync(question, options, cancellationToken);
        return Map(question, options);
    }
    public async Task DeleteQuestionAsync(Guid definitionId, Guid questionId, CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        var question = graph.Questions.SingleOrDefault(item => item.Id == questionId)
            ?? throw new NotFoundException("Question", questionId);
        graph.Version.EnsureDraft();
        await repository.DeleteQuestionAsync(question, cancellationToken);
    }

    public async Task ReorderAsync(Guid definitionId, ReorderQuestionsInput input, CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        graph.Version.EnsureDraft();
        if (input.QuestionIds.Count != graph.Questions.Count ||
            input.QuestionIds.Distinct().Count() != graph.Questions.Count ||
            input.QuestionIds.Any(id => graph.Questions.All(question => question.Id != id)))
            throw Validation("questionIds", "Supply every draft question exactly once.");
        for (var order = 0; order < input.QuestionIds.Count; order++)
            graph.Questions.Single(question => question.Id == input.QuestionIds[order]).Reorder(order);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<FormVersionResponse> PublishAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var graph = await DraftRequiredAsync(definitionId, cancellationToken);
        if (graph.Questions.Count == 0) throw Validation("questions", "A form must contain a question before publishing.");
        graph.Version.Publish(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(graph);
    }

    public async Task<FormVersionResponse> CreateDraftAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        if (await repository.FindDraftAsync(definitionId, cancellationToken) is not null)
            throw new ConflictException("A draft already exists for this form.");
        var source = await repository.FindLatestPublishedAsync(definitionId, cancellationToken)
            ?? throw new ConflictException("Publish a form version before creating the next draft.");
        var version = FormVersion.CreateDraft(definitionId, source.Version.VersionNumber + 1);
        var sectionMap = source.Sections.ToDictionary(section => section.Id,
            section => FormSection.Create(version.Id, section.Title, section.Order));
        var questions = source.Questions.Select(question => FormQuestion.Create(
            version.Id,
            question.FormSectionId.HasValue ? sectionMap[question.FormSectionId.Value].Id : null,
            question.StableKey, question.Text, question.QuestionType, question.IsRequired, question.Order)).ToArray();
        var questionMap = source.Questions.Zip(questions).ToDictionary(pair => pair.First.Id, pair => pair.Second.Id);
        var options = source.Options.Select(option => QuestionOption.Create(
            questionMap[option.FormQuestionId], option.Value, option.Label, option.Order)).ToArray();
        await repository.AddDraftGraphAsync(version, sectionMap.Values.ToArray(), questions, options, cancellationToken);
        return Map(new(source.Definition, version, sectionMap.Values.ToArray(), questions, options));
    }

    public async Task<FormVersionResponse> PreviewAsync(Guid definitionId, CancellationToken cancellationToken) =>
        Map(await repository.FindDraftAsync(definitionId, cancellationToken)
            ?? await repository.FindLatestPublishedAsync(definitionId, cancellationToken)
            ?? throw new NotFoundException("Form", definitionId));

    private async Task<FormGraph> DraftRequiredAsync(Guid id, CancellationToken token) =>
        await repository.FindDraftAsync(id, token) ?? throw new ConflictException("The form has no editable draft.");

    private static void ValidateQuestion(QuestionInput input, FormGraph graph)
    {
        if (input.SectionId.HasValue && graph.Sections.All(section => section.Id != input.SectionId.Value))
            throw Validation("sectionId", "The section does not belong to this draft.");
        var choice = input.QuestionType is QuestionType.SingleChoice or QuestionType.MultipleChoice;
        if (choice && input.Options.Count < 2) throw Validation("options", "Choice questions require at least two options.");
        if (!choice && input.Options.Count > 0) throw Validation("options", "Only choice questions may define options.");
        if (input.Options.Select(option => option.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Options.Count)
            throw Validation("options", "Option values must be unique.");
    }

    private static FormVersionResponse Map(FormGraph graph) => new(
        graph.Definition.Id, graph.Definition.Name, graph.Definition.FormType,
        graph.Version.Id, graph.Version.VersionNumber, graph.Version.Status,
        graph.Sections.OrderBy(section => section.Order).Select(section => new SectionResponse(section.Id, section.Title, section.Order)).ToArray(),
        graph.Questions.OrderBy(question => question.Order).Select(question =>
            Map(question, graph.Options.Where(option => option.FormQuestionId == question.Id).ToArray())).ToArray());
    private static QuestionResponse Map(FormQuestion question, IReadOnlyList<QuestionOption> options) => new(
        question.Id, question.StableKey, question.FormSectionId, question.Text,
        question.QuestionType, question.IsRequired, question.Order,
        options.OrderBy(option => option.Order).Select(option => new OptionResponse(option.Id, option.Value, option.Label, option.Order)).ToArray());
    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}