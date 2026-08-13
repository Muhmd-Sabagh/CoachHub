using System.Text.Json;
using CoachHub.Domain.Assessments;

namespace CoachHub.Application.Assessments;

public sealed record CreateFormInput(string Name, AssessmentFormType FormType);
public sealed record SectionInput(string Title, int Order);
public sealed record OptionInput(string Value, string Label, int Order);
public sealed record QuestionInput(
    Guid? SectionId, string Text, QuestionType QuestionType,
    bool IsRequired, int Order, IReadOnlyList<OptionInput> Options);
public sealed record ReorderQuestionsInput(IReadOnlyList<Guid> QuestionIds);
public sealed record FormSummary(Guid Id, string Name, AssessmentFormType FormType, bool IsArchived);
public sealed record OptionResponse(Guid Id, string Value, string Label, int Order);
public sealed record QuestionResponse(
    Guid Id, Guid StableKey, Guid? SectionId, string Text,
    QuestionType QuestionType, bool IsRequired, int Order,
    IReadOnlyList<OptionResponse> Options);
public sealed record SectionResponse(Guid Id, string Title, int Order);
public sealed record FormVersionResponse(
    Guid DefinitionId, string Name, AssessmentFormType FormType,
    Guid VersionId, int VersionNumber, FormVersionStatus Status,
    IReadOnlyList<SectionResponse> Sections,
    IReadOnlyList<QuestionResponse> Questions);
public sealed record FormAccessInput(string ClientCode, string FormCode);
public sealed record EligibleForm(Guid Id, string Name, AssessmentFormType FormType);
public sealed record FormAccessResponse(Guid ClientId, string ClientName, IReadOnlyList<EligibleForm> EligibleForms);
public sealed record AnswerInput(Guid QuestionId, JsonElement Value, Guid? MediaId);
public sealed record SubmitFormInput(
    string ClientCode, string FormCode, Guid FormDefinitionId,
    IReadOnlyList<AnswerInput> Answers);
public sealed record SubmissionResponse(Guid SubmissionId, DateTimeOffset SubmittedAt);

public sealed record FormGraph(
    FormDefinition Definition,
    FormVersion Version,
    IReadOnlyList<FormSection> Sections,
    IReadOnlyList<FormQuestion> Questions,
    IReadOnlyList<QuestionOption> Options);