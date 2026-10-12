using System.Text.Json.Serialization;
namespace CoachHub.Domain.Assessments;

[JsonConverter(typeof(JsonStringEnumConverter<AssessmentFormType>))]
public enum AssessmentFormType { InitialAssessment, UpdateAssessment }
[JsonConverter(typeof(JsonStringEnumConverter<FormVersionStatus>))]
public enum FormVersionStatus { Draft, Published }
[JsonConverter(typeof(JsonStringEnumConverter<QuestionType>))]
public enum QuestionType
{
    ShortText,
    LongText,
    Number,
    Date,
    Boolean,
    SingleChoice,
    MultipleChoice,
    MediaUpload
}
[JsonConverter(typeof(JsonStringEnumConverter<SubmissionSource>))]
public enum SubmissionSource { CoachHubSystem, GoogleFormsExcelImport, ManualAdminEntry }