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
    /// <summary>The forms a signed-in client may fill in now (D5): their coach's published, active forms.</summary>
    public async Task<MyFormsResponse> ListForClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await ActiveClientAsync(clientId, cancellationToken);
        var definitions = await repository.ListPublishedDefinitionsAsync(client.CoachId, cancellationToken);
        var hasInitial = await repository.HasInitialSubmissionAsync(client.Id, cancellationToken);
        var eligible = definitions
            .Where(definition => !definition.IsArchived)
            .Where(definition => definition.FormType != AssessmentFormType.InitialAssessment || !hasInitial)
            .Select(definition => new EligibleForm(definition.Id, definition.Name, definition.FormType))
            .ToArray();
        return new(client.Name, eligible);
    }

    public async Task<FormVersionResponse> GetForClientAsync(
        Guid clientId,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var forms = await ListForClientAsync(clientId, cancellationToken);
        if (forms.EligibleForms.All(form => form.Id != definitionId))
            throw new NotFoundException("Form", definitionId);
        var graph = await repository.FindLatestPublishedAsync(definitionId, cancellationToken)
            ?? throw new NotFoundException("Published form", definitionId);
        return Map(graph);
    }

    /// <summary>The owner of a file a client uploads for a form answer: the client themself.</summary>
    public async Task<MediaOwner> UploadOwnerAsync(Guid clientId, Guid? userId, CancellationToken cancellationToken)
    {
        var client = await ActiveClientAsync(clientId, cancellationToken);
        return MediaOwner.OfClient(client.CoachId, client.Id, userId);
    }

    public async Task<SubmissionResponse> SubmitForClientAsync(
        Guid clientId,
        Guid definitionId,
        MyFormSubmissionInput input,
        CancellationToken cancellationToken)
    {
        var client = await ActiveClientAsync(clientId, cancellationToken);
        var graph = await repository.FindLatestPublishedAsync(definitionId, cancellationToken)
            ?? throw new NotFoundException("Published form", definitionId);
        if (graph.Definition.CoachId != client.CoachId)
            throw new NotFoundException("Published form", definitionId);
        if (graph.Definition.IsArchived) throw new ConflictException("This form is archived.");
        if (graph.Definition.FormType == AssessmentFormType.InitialAssessment &&
            await repository.HasInitialSubmissionAsync(client.Id, cancellationToken))
            throw new ConflictException("The client has already submitted an initial assessment.");

        var answerMap = (input.Answers ?? []).GroupBy(answer => answer.QuestionId).ToArray();
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
                client.Id,
                question,
                graph.Options.Where(option => option.FormQuestionId == question.Id).ToArray(),
                inputAnswer,
                cancellationToken);
            pendingAnswers.Add((question, valueJson, inputAnswer.MediaId));
        }

        var submission = FormSubmission.Create(
            client.CoachId, client.Id, graph.Definition.Id, graph.Version.Id, graph.Definition.FormType,
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
        Guid clientId,
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
                    // Only the client's own uploads: another client's media ID must not be attachable (§62).
                    valid = media is not null && media.ClientId == clientId;
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

    /// <summary>The caller's own client record (tenant-filtered); a deactivated client may not submit.</summary>
    private async Task<CoachHub.Domain.Clients.Client> ActiveClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await repository.FindClientAsync(clientId, cancellationToken)
            ?? throw new ForbiddenException("The account is not linked to a client.");
        if (!client.IsActive) throw new ForbiddenException("The client account is inactive.");
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