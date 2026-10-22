using System.Text.Json.Serialization;
namespace CoachHub.Domain.Assessments;

[JsonConverter(typeof(JsonStringEnumConverter<AssessmentFormType>))]
public enum AssessmentFormType
{
    InitialAssessment,
    UpdateAssessment,

    /// <summary>A recurring check-in (§28), scheduled per client (P12).</summary>
    CheckIn
}
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
/// <summary>
/// A tracked measure a question records (P12): the answer also writes the client's typed entry (D3) — a weigh-in,
/// body fat, a measurement, steps, sleep, water or a progress photo.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuestionMetric>))]
public enum QuestionMetric
{
    WeightKg,
    BodyFatPercent,
    WaistCm,
    ChestCm,
    ArmCm,
    ThighCm,
    HipCm,
    NeckCm,
    CalfCm,
    Steps,
    SleepHours,
    WaterMl,
    FrontPhoto,
    SidePhoto,
    BackPhoto
}

public static class QuestionMetrics
{
    public static bool IsPhoto(QuestionMetric metric) => metric is QuestionMetric.FrontPhoto or QuestionMetric.SidePhoto or QuestionMetric.BackPhoto;

    /// <summary>Numeric measures take Number answers; photos take MediaUpload answers.</summary>
    public static QuestionType AnswerType(QuestionMetric metric) => IsPhoto(metric) ? QuestionType.MediaUpload : QuestionType.Number;
}

/// <summary>
/// Where a submission came from. <see cref="GoogleFormsExcelImport"/> marks submissions imported before the
/// importer was removed (P18A); they stay readable, and nothing creates new ones.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubmissionSource>))]
public enum SubmissionSource { CoachHubSystem, GoogleFormsExcelImport }