namespace CoachHub.Domain.Assessments;

public enum AssessmentFormType { InitialAssessment, UpdateAssessment }
public enum FormVersionStatus { Draft, Published }
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
public enum SubmissionSource { CoachHubSystem, GoogleFormsExcelImport, ManualAdminEntry }