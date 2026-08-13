using System.Globalization;
using System.Text.Json;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments;

public sealed class FormSubmissionService(
    IFormRepository repository,
    IMediaRepository mediaRepository,
    TimeProvider timeProvider)
{
    public async Task<FormAccessResponse> ValidateAccessAsync(
        FormAccessInput input,
        CancellationToken cancellationToken)
    {
        var client = await FindClientAsync(input, cancellationToken);
        var definitions = await repository.ListPublishedDefinitionsAsync(cancellationToken);
        var hasInitial = await repository.HasInitialSubmissionAsync(client.Id, cancellationToken);
        var eligible = definitions
            .Where(definition => !definition.IsArchived)
            .Where(definition => definition.FormType != AssessmentFormType.InitialAssessment || !hasInitial)
            .Select(definition => new EligibleForm(definition.Id, definition.Name, definition.FormType))
            .ToArray();
        return new(client.Id, client.Name, eligible);
    }

    public async Task<FormVersionResponse> GetPublishedAsync(
        FormAccessInput input,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(input, cancellationToken);
        if (access.EligibleForms.All(form => form.Id != definitionId))
            throw new ConflictException("The client is not eligible for this form.");
        var graph = await repository.FindLatestPublishedAsync(definitionId, cancellationToken)
            ?? throw new NotFoundException("Published form", definitionId);
        return Map(graph);
    }

    public async Task<SubmissionResponse> SubmitAsync(
        SubmitFormInput input,
        CancellationToken cancellationToken)
    {
        var client = await FindClientAsync(new(input.ClientCode, input.FormCode), cancellationToken);
        var graph = await repository.FindLatestPublishedAsync(input.FormDefinitionId, cancellationToken)
            ?? throw new NotFoundException("Published form", input.FormDefinitionId);
        if (graph.Definition.IsArchived) throw new ConflictException("This form is archived.");
        if (graph.Definition.FormType == AssessmentFormType.InitialAssessment &&
            await repository.HasInitialSubmissionAsync(client.Id, cancellationToken))
            throw new ConflictException("The client has already submitted an initial assessment.");

        var answerMap = input.Answers.GroupBy(answer => answer.QuestionId).ToArray();
        if (answerMap.Any(group => group.Count() > 1) ||
            answerMap.Any(group => graph.Questions.All(question => question.Id != group.Key)))
            throw Validation("answers", "Answers must reference each published question at most once.");

        var pendingAnswers = new List<(FormQuestion Question, string ValueJson, Guid? MediaId)>();
        foreach (var question in graph.Questions)
        {
            var inputAnswer = answerMap.SingleOrDefault(group => group.Key == question.Id)?.Single();
            if (inputAnswer is null)
            {
                if (question.IsRequired) throw Validation(question.Id.ToString(), "An answer is required.");
                continue;
            }
            var valueJson = await ValidateAnswerAsync(
                question,
                graph.Options.Where(option => option.FormQuestionId == question.Id).ToArray(),
                inputAnswer,
                cancellationToken);
            pendingAnswers.Add((question, valueJson, inputAnswer.MediaId));
        }

        var submission = FormSubmission.Create(
            client.Id, graph.Definition.Id, graph.Version.Id, graph.Definition.FormType,
            SubmissionSource.CoachHubSystem, timeProvider.GetUtcNow());
        var answers = pendingAnswers.Select(answer => FormAnswer.Create(
            submission.Id,
            answer.Question,
            answer.ValueJson,
            answer.MediaId)).ToArray();
        client.RecordAssessmentSubmission(graph.Definition.FormType);
        await repository.SubmitAsync(submission, answers, cancellationToken);
        return new(submission.Id, submission.SubmittedAt);
    }

    private async Task<string> ValidateAnswerAsync(
        FormQuestion question,
        IReadOnlyList<QuestionOption> options,
        AnswerInput answer,
        CancellationToken cancellationToken)
    {
        var value = answer.Value;
        bool valid;
        switch (question.QuestionType)
        {
            case QuestionType.ShortText:
            case QuestionType.LongText:
                valid = value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()) &&
                    value.GetString()!.Length <= (question.QuestionType == QuestionType.ShortText ? 1000 : 10000);
                break;
            case QuestionType.Number:
                valid = value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _);
                break;
            case QuestionType.Date:
                valid = value.ValueKind == JsonValueKind.String &&
                    DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _);
                break;
            case QuestionType.Boolean:
                valid = value.ValueKind is JsonValueKind.True or JsonValueKind.False;
                break;
            case QuestionType.SingleChoice:
                valid = value.ValueKind == JsonValueKind.String &&
                    options.Any(option => option.Value == value.GetString());
                break;
            case QuestionType.MultipleChoice:
                valid = value.ValueKind == JsonValueKind.Array &&
                    value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) &&
                    value.EnumerateArray().Select(item => item.GetString()).Distinct().Count() == value.GetArrayLength() &&
                    value.EnumerateArray().All(item => options.Any(option => option.Value == item.GetString()));
                break;
            case QuestionType.MediaUpload:
                valid = answer.MediaId.HasValue && value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.String;
                if (valid)
                {
                    var media = await mediaRepository.FindAsync(answer.MediaId!.Value, cancellationToken);
                    valid = media is not null;
                }
                break;
            default:
                valid = false;
                break;
        }
        if (question.QuestionType != QuestionType.MediaUpload && answer.MediaId.HasValue) valid = false;
        if (!valid) throw Validation(question.Id.ToString(), $"Invalid {question.QuestionType} answer.");
        return question.QuestionType == QuestionType.MediaUpload
            ? JsonSerializer.Serialize(answer.MediaId)
            : value.GetRawText();
    }

    private async Task<CoachHub.Domain.Clients.Client> FindClientAsync(
        FormAccessInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.ClientCode) || string.IsNullOrWhiteSpace(input.FormCode))
            throw Validation("codes", "Client code and form code are required.");
        var client = await repository.FindClientByCodesAsync(
            input.ClientCode.Trim().ToUpperInvariant(), input.FormCode.Trim().ToUpperInvariant(), cancellationToken);
        if (client is null || !client.IsActive)
            throw new NotFoundException("Client access", "invalid");
        return client;
    }

    private static FormVersionResponse Map(FormGraph graph) => new(
        graph.Definition.Id, graph.Definition.Name, graph.Definition.FormType,
        graph.Version.Id, graph.Version.VersionNumber, graph.Version.Status,
        graph.Sections.OrderBy(section => section.Order).Select(section => new SectionResponse(section.Id, section.Title, section.Order)).ToArray(),
        graph.Questions.OrderBy(question => question.Order).Select(question => new QuestionResponse(
            question.Id, question.StableKey, question.FormSectionId, question.Text,
            question.QuestionType, question.IsRequired, question.Order,
            graph.Options.Where(option => option.FormQuestionId == question.Id)
                .OrderBy(option => option.Order)
                .Select(option => new OptionResponse(option.Id, option.Value, option.Label, option.Order)).ToArray())).ToArray());
    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}