using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments.Importing;

public sealed class AssessmentImportService(
    IFormImportRepository repository,
    IFormRepository formRepository,
    IAssessmentWorkbookParser parser)
{
    public async Task<ImportProfileResponse> CreateProfileAsync(
        SaveImportProfileInput input, CancellationToken cancellationToken)
    {
        var profile = FormImportProfile.Create(
            input.FormDefinitionId, input.Name, input.SheetName,
            input.FormCodeHeader, input.TimestampHeader, input.ExternalIdHeader);
        var mappings = await ValidateAndCreateMappingsAsync(profile, input.Mappings, cancellationToken);
        await repository.AddProfileAsync(profile, mappings, cancellationToken);
        return Map(profile, mappings);
    }

    public async Task<ImportProfileResponse> UpdateProfileAsync(
        Guid id, SaveImportProfileInput input, CancellationToken cancellationToken)
    {
        var existing = await repository.FindProfileAsync(id, cancellationToken)
            ?? throw new NotFoundException("Assessment import profile", id);
        if (existing.Profile.FormDefinitionId != input.FormDefinitionId)
            throw Validation("formDefinitionId", "An import profile cannot be moved to another form.");
        existing.Profile.Update(input.Name, input.SheetName, input.FormCodeHeader,
            input.TimestampHeader, input.ExternalIdHeader);
        var mappings = await ValidateAndCreateMappingsAsync(existing.Profile, input.Mappings, cancellationToken);
        await repository.ReplaceProfileAsync(existing.Profile, mappings, cancellationToken);
        return Map(existing.Profile, mappings);
    }

    public async Task<AssessmentImportSummary> ImportAsync(
        Guid profileId, Stream workbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var configured = await repository.FindProfileAsync(profileId, cancellationToken)
            ?? throw new NotFoundException("Assessment import profile", profileId);
        var graph = await formRepository.FindLatestPublishedAsync(
            configured.Profile.FormDefinitionId, cancellationToken)
            ?? throw new ConflictException("The mapped assessment form has no published version.");
        var sheet = await parser.ParseAsync(workbook, configured.Profile.SheetName, cancellationToken);
        var diagnostics = new List<ImportDiagnostic>();
        var reserved = new[]
        {
            configured.Profile.FormCodeHeader,
            configured.Profile.TimestampHeader,
            configured.Profile.ExternalIdHeader
        }.Where(value => value is not null).Select(value => NormalizeHeader(value!)).ToHashSet();
        var mappings = configured.Mappings.ToDictionary(item => NormalizeHeader(item.Header));
        var unmappedHeaders = sheet.Headers
            .Where(header => !reserved.Contains(NormalizeHeader(header)) && !mappings.ContainsKey(NormalizeHeader(header)))
            .ToArray();
        diagnostics.AddRange(unmappedHeaders.Select(header =>
            new ImportDiagnostic(1, ImportRowStatus.UnmappedQuestion,
                "Workbook column is not mapped and was ignored.", header)));

        var imported = 0;
        var duplicate = 0;
        var invalid = 0;
        var unknownClient = 0;
        foreach (var row in sheet.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var formCode = Cell(row, configured.Profile.FormCodeHeader)?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(formCode))
                {
                    unknownClient++;
                    diagnostics.Add(new(row.RowNumber, ImportRowStatus.UnknownClientOrCode,
                        "The form code is missing or unknown."));
                    continue;
                }
                var client = await repository.FindClientByFormCodeAsync(formCode, cancellationToken);
                if (client is null || !client.IsActive)
                {
                    unknownClient++;
                    diagnostics.Add(new(row.RowNumber, ImportRowStatus.UnknownClientOrCode,
                        "The form code is missing or unknown."));
                    continue;
                }
                if (!TryTimestamp(Cell(row, configured.Profile.TimestampHeader), out var submittedAt))
                    throw new RowImportException(configured.Profile.TimestampHeader, "The timestamp is missing or invalid.");
                if (graph.Definition.FormType == AssessmentFormType.InitialAssessment &&
                    await formRepository.HasInitialSubmissionAsync(client.Id, cancellationToken))
                {
                    duplicate++;
                    diagnostics.Add(new(row.RowNumber, ImportRowStatus.SkippedDuplicate,
                        "An initial assessment already exists for this client."));
                    continue;
                }

                var answers = new List<(FormQuestion Question, string Json, string? ExternalMediaUrl)>();
                foreach (var mapping in configured.Mappings)
                {
                    var question = graph.Questions.SingleOrDefault(item => item.StableKey == mapping.QuestionStableKey)
                        ?? throw new RowImportException(mapping.Header, "The mapped stable question is not present in the published form.");
                    var raw = Cell(row, mapping.Header);
                    var converted = ConvertAnswer(question,
                        graph.Options.Where(option => option.FormQuestionId == question.Id).ToArray(), raw, mapping.Header);
                    if (converted is not null) answers.Add((question, converted.Value.Json, converted.Value.ExternalMediaUrl));
                }
                foreach (var question in graph.Questions.Where(item => item.IsRequired))
                {
                    if (answers.All(answer => answer.Question.Id != question.Id))
                        throw new RowImportException(null, $"Required question '{question.Text}' has no mapped answer.");
                }

                var externalId = configured.Profile.ExternalIdHeader is null
                    ? null : Cell(row, configured.Profile.ExternalIdHeader)?.Trim();
                var fingerprint = Fingerprint(graph.Definition.Id, client.Id, submittedAt, externalId, answers);
                if (await repository.ImportFingerprintExistsAsync(fingerprint, cancellationToken))
                {
                    duplicate++;
                    diagnostics.Add(new(row.RowNumber, ImportRowStatus.SkippedDuplicate,
                        "This external response was imported previously."));
                    continue;
                }

                var submission = FormSubmission.Create(
                    client.Id, graph.Definition.Id, graph.Version.Id, graph.Definition.FormType,
                    SubmissionSource.GoogleFormsExcelImport, submittedAt, fingerprint, externalId);
                var entities = answers.Select(answer => FormAnswer.Create(
                    submission.Id, answer.Question, answer.Json, null, answer.ExternalMediaUrl)).ToArray();
                client.RecordAssessmentSubmission(graph.Definition.FormType);
                if (!await repository.TrySubmitAsync(submission, entities, cancellationToken))
                {
                    duplicate++;
                    diagnostics.Add(new(row.RowNumber, ImportRowStatus.SkippedDuplicate,
                        "This response conflicts with an existing import or initial assessment."));
                    continue;
                }
                imported++;
            }
            catch (RowImportException exception)
            {
                invalid++;
                diagnostics.Add(new(row.RowNumber, ImportRowStatus.Invalid, exception.Message, exception.Column));
            }
        }
        return new(imported, duplicate, invalid, unmappedHeaders.Length, unknownClient, diagnostics);
    }

    private async Task<FormImportColumnMapping[]> ValidateAndCreateMappingsAsync(
        FormImportProfile profile, IReadOnlyList<ImportColumnMappingInput> inputs, CancellationToken token)
    {
        if (inputs.Count == 0) throw Validation("mappings", "At least one question mapping is required.");
        var graph = await formRepository.FindLatestPublishedAsync(profile.FormDefinitionId, token)
            ?? throw new ConflictException("Publish the form before configuring an import profile.");
        if (inputs.Select(item => NormalizeHeader(item.Header)).Distinct().Count() != inputs.Count)
            throw Validation("mappings", "Mapped headers must be unique after normalization.");
        if (inputs.Select(item => item.ExternalColumnKey.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputs.Count)
            throw Validation("mappings", "External column keys must be unique.");
        if (inputs.Select(item => item.QuestionStableKey).Distinct().Count() != inputs.Count)
            throw Validation("mappings", "Each question may be mapped only once.");
        var reserved = new[] { profile.FormCodeHeader, profile.TimestampHeader, profile.ExternalIdHeader }
            .Where(value => value is not null).Select(value => NormalizeHeader(value!)).ToArray();
        if (reserved.Distinct().Count() != reserved.Length || inputs.Any(item => reserved.Contains(NormalizeHeader(item.Header))))
            throw Validation("headers", "Reserved and question headers must be distinct.");
        if (inputs.Any(input => graph.Questions.All(question => question.StableKey != input.QuestionStableKey)))
            throw Validation("mappings", "Every mapping must reference a stable key in the published form.");
        return inputs.Select(input => FormImportColumnMapping.Create(
            profile.Id, input.ExternalColumnKey, input.Header, input.QuestionStableKey)).ToArray();
    }

    private static (string Json, string? ExternalMediaUrl)? ConvertAnswer(
        FormQuestion question, IReadOnlyList<QuestionOption> options, string? raw, string header)
    {
        raw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (raw is null)
        {
            if (question.IsRequired) throw new RowImportException(header, "A required answer is empty.");
            return null;
        }
        switch (question.QuestionType)
        {
            case QuestionType.ShortText:
                if (raw.Length > 1000) throw new RowImportException(header, "Short-text answer exceeds 1,000 characters.");
                return (JsonSerializer.Serialize(raw), null);
            case QuestionType.LongText:
                if (raw.Length > 10000) throw new RowImportException(header, "Long-text answer exceeds 10,000 characters.");
                return (JsonSerializer.Serialize(raw), null);
            case QuestionType.Number:
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    throw new RowImportException(header, "A numeric answer is invalid.");
                return (JsonSerializer.Serialize(number), null);
            case QuestionType.Date:
                DateOnly date;
                if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    date = parsedDate;
                else if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
                    date = DateOnly.FromDateTime(dateTime.Date);
                else
                    throw new RowImportException(header, "A date answer is invalid.");
                return (JsonSerializer.Serialize(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), null);
            case QuestionType.Boolean:
                if (!TryBoolean(raw, out var boolean))
                    throw new RowImportException(header, "A boolean answer is invalid.");
                return (JsonSerializer.Serialize(boolean), null);
            case QuestionType.SingleChoice:
                return (JsonSerializer.Serialize(OptionValue(raw, options, header)), null);
            case QuestionType.MultipleChoice:
                var values = raw.Split([',', '،', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => OptionValue(value, options, header)).Distinct().ToArray();
                if (values.Length == 0) throw new RowImportException(header, "A multiple-choice answer is empty.");
                return (JsonSerializer.Serialize(values), null);
            case QuestionType.MediaUpload:
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                    !(uri.Host.Equals("google.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)))
                    throw new RowImportException(header, "Media must be a Google HTTPS URL.");
                return (JsonSerializer.Serialize(raw), raw);
            default:
                throw new RowImportException(header, "The question type is not supported by the importer.");
        }
    }

    private static string OptionValue(string raw, IReadOnlyList<QuestionOption> options, string header)
    {
        var option = options.SingleOrDefault(item =>
            item.Value.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
            item.Label.Equals(raw, StringComparison.OrdinalIgnoreCase));
        return option?.Value ?? throw new RowImportException(header, "A choice answer does not match a configured option.");
    }
    private static bool TryBoolean(string raw, out bool value)
    {
        if (bool.TryParse(raw, out value)) return true;
        var normalized = raw.Trim();
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("نعم")) { value = true; return true; }
        if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("لا")) { value = false; return true; }
        return false;
    }
    private static bool TryTimestamp(string? raw, out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value)) return true;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 0 and < 2958466)
        {
            value = new DateTimeOffset(DateTime.FromOADate(serial), TimeSpan.Zero);
            return true;
        }
        return false;
    }
    private static string Fingerprint(
        Guid definitionId, Guid clientId, DateTimeOffset submittedAt, string? externalId,
        IReadOnlyList<(FormQuestion Question, string Json, string? ExternalMediaUrl)> answers)
    {
        var canonical = string.IsNullOrWhiteSpace(externalId)
            ? $"{definitionId:N}|{clientId:N}|{submittedAt.UtcTicks}|" +
                string.Join('|', answers.OrderBy(item => item.Question.StableKey)
                    .Select(item => $"{item.Question.StableKey:N}={item.Json}"))
            : $"{definitionId:N}|{clientId:N}|EXTERNAL|{externalId.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    private static string? Cell(ParsedWorkbookRow row, string header) =>
        row.Cells.TryGetValue(NormalizeHeader(header), out var value) ? value : null;
    public static string NormalizeHeader(string value) =>
        string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim().ToUpperInvariant();
    private static ImportProfileResponse Map(
        FormImportProfile profile, IReadOnlyList<FormImportColumnMapping> mappings) =>
        new(profile.Id, profile.FormDefinitionId, profile.Name, profile.SheetName,
            profile.FormCodeHeader, profile.TimestampHeader, profile.ExternalIdHeader,
            mappings.Select(item => new ImportColumnMappingResponse(
                item.Id, item.ExternalColumnKey, item.Header, item.QuestionStableKey)).ToArray());
    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private sealed class RowImportException(string? column, string message) : Exception(message)
    {
        public string? Column { get; } = column;
    }
}