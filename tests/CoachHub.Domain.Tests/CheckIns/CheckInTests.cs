using CoachHub.Domain.Assessments;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;

namespace CoachHub.Domain.Tests.CheckIns;

/// <summary>Check-in schedules, the review workflow (§28, §29), metric-bound questions and the status (§39).</summary>
public sealed class CheckInTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();

    [Fact]
    public void A_schedule_falls_on_its_weekday_every_interval()
    {
        // Starting Wednesday 2026-10-21, due on Sundays every 2 weeks: 25 Oct, 8 Nov, 22 Nov …
        var schedule = CheckInSchedule.Create(Coach, Client, Guid.NewGuid(), 2, DayOfWeek.Sunday, new DateOnly(2026, 10, 21), Now);
        Assert.Equal(new DateOnly(2026, 10, 25), schedule.FirstDueOn);
        Assert.Equal(new DateOnly(2026, 10, 25), schedule.DueOnOrAfter(new DateOnly(2026, 10, 1)));
        Assert.Equal(new DateOnly(2026, 11, 8), schedule.DueOnOrAfter(new DateOnly(2026, 10, 26)));
        Assert.Equal(new DateOnly(2026, 11, 8), schedule.DueOnOrAfter(new DateOnly(2026, 11, 8)));
        Assert.Null(schedule.DueOnOrBefore(new DateOnly(2026, 10, 24)));
        Assert.Equal(new DateOnly(2026, 11, 8), schedule.DueOnOrBefore(new DateOnly(2026, 11, 20)));
        Assert.Equal([new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 22)],
            schedule.DueDatesBetween(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30)));

        // A start on the due weekday is the first due date.
        Assert.Equal(new DateOnly(2026, 10, 21), CheckInSchedule.Create(Coach, Client, Guid.NewGuid(), 1, DayOfWeek.Wednesday,
            new DateOnly(2026, 10, 21), Now).FirstDueOn);
        Assert.Throws<ArgumentException>(() => CheckInSchedule.Create(Coach, Client, Guid.NewGuid(), 3, DayOfWeek.Monday, new DateOnly(2026, 10, 21), Now));
        Assert.Throws<ArgumentException>(() => CheckInSchedule.Create(Coach, Client, Guid.Empty, 1, DayOfWeek.Monday, new DateOnly(2026, 10, 21), Now));
    }

    [Fact]
    public void A_check_in_is_reviewed_and_resubmitted_after_requested_changes()
    {
        var first = Guid.NewGuid();
        var checkIn = CheckIn.Submit(Coach, Client, Guid.NewGuid(), new DateOnly(2026, 10, 18), first, new DateOnly(2026, 10, 19), Now);
        Assert.Equal((CheckInStatus.AwaitingReview, true, first), (checkIn.Status, checkIn.IsLate, checkIn.SubmissionId));

        // Changes must say what to change; then the client resubmits and it waits for review again.
        Assert.Throws<ArgumentException>(() => checkIn.RequestChanges(Reviewer, " ", Now));
        Assert.Throws<InvalidOperationException>(() => checkIn.Resubmit(Guid.NewGuid(), new DateOnly(2026, 10, 19), Now));
        checkIn.RequestChanges(Reviewer, "Add your side photo.", Now);
        Assert.Equal((CheckInStatus.ChangesRequested, "Add your side photo."), (checkIn.Status, checkIn.Replies.Single().Text));
        var second = Guid.NewGuid();
        checkIn.Resubmit(second, new DateOnly(2026, 10, 20), Now.AddDays(1));
        Assert.Equal((CheckInStatus.AwaitingReview, second, 2), (checkIn.Status, checkIn.SubmissionId, checkIn.Submissions.Count));
        Assert.True(checkIn.IsLate);

        checkIn.Reply(Reviewer, "Great progress!", Now.AddDays(1));
        checkIn.ChangePrivateNotes(" Watch the knee. ");
        checkIn.Approve(Reviewer, null, Now.AddDays(2));
        Assert.Equal((CheckInStatus.Approved, 2, "Watch the knee."), (checkIn.Status, checkIn.Replies.Count, checkIn.PrivateNotes));
        Assert.Equal((Reviewer, Now.AddDays(2)), (checkIn.ReviewedByUserId!.Value, checkIn.ReviewedAt!.Value));
        Assert.Throws<ArgumentException>(() => checkIn.Reply(Reviewer, new string('x', CheckIn.MaximumTextLength + 1), Now));
        Assert.False(CheckIn.Submit(Coach, Client, Guid.NewGuid(), new DateOnly(2026, 10, 25), Guid.NewGuid(), new DateOnly(2026, 10, 24), Now).IsLate);
    }

    [Fact]
    public void Metric_questions_take_the_matching_answer_type()
    {
        var version = Guid.NewGuid();
        var weight = FormQuestion.Create(version, null, Guid.NewGuid(), "Weight this morning", QuestionType.Number, true, 0, QuestionMetric.WeightKg);
        Assert.Equal(QuestionMetric.WeightKg, weight.Metric);
        Assert.Equal(QuestionMetric.FrontPhoto,
            FormQuestion.Create(version, null, Guid.NewGuid(), "Front photo", QuestionType.MediaUpload, false, 1, QuestionMetric.FrontPhoto).Metric);
        Assert.Throws<ArgumentException>(() =>
            FormQuestion.Create(version, null, Guid.NewGuid(), "Weight", QuestionType.ShortText, true, 0, QuestionMetric.WeightKg));
        Assert.Throws<ArgumentException>(() =>
            FormQuestion.Create(version, null, Guid.NewGuid(), "Photo", QuestionType.Number, true, 0, QuestionMetric.BackPhoto));
        weight.Update(null, "Weight", QuestionType.Number, true, 0);
        Assert.Null(weight.Metric);
    }

    [Fact]
    public void Check_ins_drive_the_status_after_plans_and_before_on_track()
    {
        var today = new DateOnly(2026, 10, 21);
        var client = Domain.Clients.Client.Create(Coach, "CLIENT01", "Sam", null, null, today);
        Assert.Equal(ClientStatus.OnTrack, client.GetStatus(today));

        client.SetCheckInState(today.AddDays(-1), false);
        Assert.Equal(ClientStatus.CheckInOverdue, client.GetStatus(today));
        client.SetCheckInState(today, false);
        Assert.Equal(ClientStatus.OnTrack, client.GetStatus(today));      // due today is not overdue

        // A check-in waiting for the coach is "needs review", ahead of overdue.
        client.SetCheckInState(today.AddDays(-1), true);
        Assert.Equal(ClientStatus.NeedsReview, client.GetStatus(today));
        client.OverrideStatus(ClientStatus.OnTrack, null, today);
        Assert.Equal(ClientStatus.OnTrack, client.GetStatus(today));

        // A check-in submission leaves the plans alone.
        client.ClearStatusOverride();
        client.SetCheckInState(null, false);
        client.RecordAssessmentSubmission(AssessmentFormType.CheckIn);
        Assert.Equal((PlanWorkflowStatus.NotStarted, ClientStatus.OnTrack), (client.DietStatus, client.GetStatus(today)));
    }
}
