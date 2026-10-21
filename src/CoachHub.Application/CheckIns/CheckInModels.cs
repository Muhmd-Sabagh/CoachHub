using System.Text.Json.Serialization;
using CoachHub.Application.Assessments;
using CoachHub.Domain.CheckIns;

namespace CoachHub.Application.CheckIns;

/// <summary>The coach's check-in schedule for a client: a published check-in form, every 1, 2 or 4 weeks on a weekday.</summary>
public sealed record CheckInScheduleInput(
    Guid FormDefinitionId, int IntervalWeeks,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek DueWeekday,
    DateOnly? StartsOn = null);

/// <summary>Where the client's next check-in stands (§29): not open yet, open (pending), or overdue.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckInDueState>))]
public enum CheckInDueState
{
    Upcoming,
    Pending,
    Overdue
}

public sealed record CheckInScheduleResponse(
    Guid ClientId, Guid FormDefinitionId, string FormName, int IntervalWeeks,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek DueWeekday, DateOnly StartsOn,
    DateOnly? NextDueOn, DateOnly? OpensOn, CheckInDueState? State);

public sealed record CheckInQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? ClientId { get; init; }
    public CheckInStatus? Status { get; init; }

    public CheckInQuery Normalize() => this with { PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 100) };
}

public sealed record CheckInDueQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Pending (open or upcoming) or Overdue; all scheduled clients when left out.</summary>
    public CheckInDueState? State { get; init; }

    public CheckInDueQuery Normalize() => this with { PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 100) };
}

/// <summary>A scheduled client's next check-in, for the coach's pending and overdue lists (§29).</summary>
public sealed record CheckInDueResponse(Guid ClientId, string ClientName, DateOnly DueOn, CheckInDueState State, int DaysOverdue);

public sealed record CheckInSummary(
    Guid Id, Guid ClientId, string ClientName, Guid FormDefinitionId, string FormName, DateOnly DueOn, DateTimeOffset SubmittedAt,
    bool IsLate, CheckInStatus Status, int SubmissionCount, int ReplyCount);

public sealed record CheckInReplyResponse(Guid Id, string Text, string? AuthorName, DateTimeOffset CreatedAt);
public sealed record CheckInSubmissionResponse(Guid FormSubmissionId, DateTimeOffset SubmittedAt, bool IsLate);

/// <summary>
/// A check-in for the coach: the latest submission's answers, every submission, the replies the client sees, and the
/// coach's private notes.
/// </summary>
public sealed record CheckInDetail(
    CheckInSummary CheckIn, IReadOnlyList<AssessmentAnswerResponse> Answers, IReadOnlyList<CheckInSubmissionResponse> Submissions,
    IReadOnlyList<CheckInReplyResponse> Replies, string? PrivateNotes, DateTimeOffset? ReviewedAt);

/// <summary>A check-in as the client sees it: no private notes.</summary>
public sealed record MyCheckInResponse(
    Guid Id, Guid FormDefinitionId, string FormName, DateOnly DueOn, DateTimeOffset SubmittedAt, bool IsLate, CheckInStatus Status,
    IReadOnlyList<CheckInSubmissionResponse> Submissions, IReadOnlyList<CheckInReplyResponse> Replies,
    IReadOnlyList<AssessmentAnswerResponse> Answers);

/// <summary>
/// The client's check-ins: the schedule and next due date, the check-in to resubmit (after "request changes"), and the
/// history newest first.
/// </summary>
public sealed record MyCheckInsResponse(
    CheckInScheduleResponse? Schedule, Guid? ResubmitCheckInId, IReadOnlyList<MyCheckInResponse> History);

public sealed record CheckInReplyInput(string Text);
public sealed record CheckInNotesInput(string? Notes);
public sealed record CheckInReviewInput(string? Message);

/// <summary>Where a check-in submission goes: a resubmission of <paramref name="Existing"/>, or a new check-in due on <paramref name="DueOn"/>.</summary>
public sealed record CheckInTarget(CheckIn? Existing, DateOnly DueOn, CheckInSchedule? Schedule);

/// <summary>Check-ins due and done in a period (§30): due dates up to today, and how many have a check-in.</summary>
public sealed record CheckInAdherence(int Due, int Completed, int Late, decimal? Percent);
