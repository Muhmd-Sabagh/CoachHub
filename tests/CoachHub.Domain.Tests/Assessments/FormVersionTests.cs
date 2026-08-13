using CoachHub.Domain.Assessments;

namespace CoachHub.Domain.Tests.Assessments;

public sealed class FormVersionTests
{
    [Fact]
    public void Published_versions_are_immutable()
    {
        var version = FormVersion.CreateDraft(Guid.NewGuid(), 1);
        version.Publish(DateTimeOffset.UtcNow);
        Assert.Equal(FormVersionStatus.Published, version.Status);
        Assert.Throws<InvalidOperationException>(version.EnsureDraft);
    }

    [Fact]
    public void Initial_submission_sets_unique_client_marker_but_update_does_not()
    {
        var clientId = Guid.NewGuid();
        var initial = FormSubmission.Create(
            clientId, Guid.NewGuid(), Guid.NewGuid(), AssessmentFormType.InitialAssessment,
            SubmissionSource.CoachHubSystem, DateTimeOffset.UtcNow);
        var update = FormSubmission.Create(
            clientId, Guid.NewGuid(), Guid.NewGuid(), AssessmentFormType.UpdateAssessment,
            SubmissionSource.CoachHubSystem, DateTimeOffset.UtcNow);
        Assert.Equal(clientId, initial.InitialClientId);
        Assert.Null(update.InitialClientId);
    }

    [Fact]
    public void Answer_snapshots_question_identity_text_and_type()
    {
        var question = FormQuestion.Create(
            Guid.NewGuid(), null, Guid.NewGuid(), "Original text",
            QuestionType.Number, true, 0);
        var answer = FormAnswer.Create(Guid.NewGuid(), question, "42", null);
        Assert.Equal(question.StableKey, answer.QuestionStableKey);
        Assert.Equal("Original text", answer.QuestionTextSnapshot);
        Assert.Equal(QuestionType.Number, answer.QuestionTypeSnapshot);
    }

    [Fact]
    public void Imported_media_rejects_lookalike_google_hosts()
    {
        var version = FormVersion.CreateDraft(Guid.NewGuid(), 1);
        var question = FormQuestion.Create(
            version.Id, null, Guid.NewGuid(), "Photo", QuestionType.MediaUpload, true, 0);
        Assert.Throws<ArgumentException>(() => FormAnswer.Create(
            Guid.NewGuid(), question, "\"https://evilgoogle.com/photo\"", null,
            "https://evilgoogle.com/photo"));
    }}