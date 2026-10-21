using System.Text.Json;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments;

/// <param name="CoachId">Required when an administrator creates the form; ignored for coaches.</param>
public sealed record CreateFormInput(string Name, AssessmentFormType FormType, Guid? CoachId = null);
public sealed record SectionInput(string Title, int Order);
public sealed record OptionInput(string Value, string Label, int Order);
/// <param name="Metric">The measure the answer records (P12): a number question for a measure, a media upload for a photo.</param>
public sealed record QuestionInput(
    Guid? SectionId, string Text, QuestionType QuestionType,
    bool IsRequired, int Order, IReadOnlyList<OptionInput> Options, QuestionMetric? Metric = null);
public sealed record ReorderQuestionsInput(IReadOnlyList<Guid> QuestionIds);
public sealed record FormSummary(Guid Id, string Name, AssessmentFormType FormType, bool IsArchived);
public sealed record OptionResponse(Guid Id, string Value, string Label, int Order);
public sealed record QuestionResponse(
    Guid Id, Guid StableKey, Guid? SectionId, string Text,
    QuestionType QuestionType, bool IsRequired, int Order,
    IReadOnlyList<OptionResponse> Options, QuestionMetric? Metric = null);
public sealed record SectionResponse(Guid Id, string Title, int Order);
public sealed record FormVersionResponse(
    Guid DefinitionId, string Name, AssessmentFormType FormType,
    Guid VersionId, int VersionNumber, FormVersionStatus Status,
    IReadOnlyList<SectionResponse> Sections,
    IReadOnlyList<QuestionResponse> Questions);
public sealed record EligibleForm(Guid Id, string Name, AssessmentFormType FormType);
/// <summary>The forms a signed-in client can fill in (D5).</summary>
public sealed record MyFormsResponse(string ClientName, IReadOnlyList<EligibleForm> EligibleForms);
public sealed record AnswerInput(Guid QuestionId, JsonElement Value, Guid? MediaId);
/// <param name="Date">The client's local day of the answers (for measures they record); today (UTC) when left out.</param>
public sealed record MyFormSubmissionInput(IReadOnlyList<AnswerInput> Answers, DateOnly? Date = null);
public sealed record SubmissionResponse(Guid SubmissionId, DateTimeOffset SubmittedAt);

public sealed record FormGraph(
    FormDefinition Definition,
    FormVersion Version,
    IReadOnlyList<FormSection> Sections,
    IReadOnlyList<FormQuestion> Questions,
    IReadOnlyList<QuestionOption> Options);