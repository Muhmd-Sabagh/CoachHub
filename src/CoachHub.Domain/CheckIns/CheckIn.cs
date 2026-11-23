using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.CheckIns;

/// <summary>
/// A client's check-in schedule (§28, P12): which check-in form, every 1, 2 or 4 weeks, due on a weekday, starting on a
/// date. Written by the coach; one per client. Since P22 it is in force from <see cref="InForceFrom"/>: the days before
/// belong to the closed <see cref="CheckInSchedulePeriod"/>s that the earlier schedules left.
/// </summary>
public sealed class CheckInSchedule : Entity, ITenantOwned
{
    public static readonly int[] Intervals = [1, 2, 4];

    private CheckInSchedule() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid FormDefinitionId { get; private set; }
    public int IntervalWeeks { get; private set; }
    public DayOfWeek DueWeekday { get; private set; }
    public DateOnly StartsOn { get; private set; }

    /// <summary>
    /// The first day this schedule counts for (P22); null when no earlier schedule covers any day before it. Days
    /// before it belong to the client's closed periods.
    /// </summary>
    public DateOnly? InForceFrom { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <param name="inForceFrom">The day after the client's last closed period, if any (P22).</param>
    public static CheckInSchedule Create(
        Guid coachId, Guid clientId, Guid formDefinitionId, int intervalWeeks, DayOfWeek dueWeekday, DateOnly startsOn,
        DateTimeOffset now, DateOnly? inForceFrom = null)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        var schedule = new CheckInSchedule { CoachId = TenantKey.Required(coachId), ClientId = clientId, InForceFrom = inForceFrom };
        schedule.Set(formDefinitionId, intervalWeeks, dueWeekday, startsOn, now);
        return schedule;
    }

    /// <summary>
    /// Changes the schedule from <paramref name="today"/> on (P22). The days before keep the terms they had: the
    /// returned period records them, or null when the old terms had no day in force yet.
    /// </summary>
    public CheckInSchedulePeriod? Change(
        Guid formDefinitionId, int intervalWeeks, DayOfWeek dueWeekday, DateOnly startsOn, DateOnly today, DateTimeOffset now)
    {
        var closed = Close(today.AddDays(-1), now);
        Set(formDefinitionId, intervalWeeks, dueWeekday, startsOn, now);
        if (closed is not null) InForceFrom = today;
        return closed;
    }

    /// <summary>
    /// The schedule's terms up to <paramref name="lastDay"/>, for when it changes or ends (P22); null when no day up to
    /// then was in force.
    /// </summary>
    public CheckInSchedulePeriod? Close(DateOnly lastDay, DateTimeOffset now)
    {
        var first = InForceFrom ?? StartsOn;
        return lastDay < first ? null : CheckInSchedulePeriod.Record(this, first, lastDay, now);
    }

    private void Set(Guid formDefinitionId, int intervalWeeks, DayOfWeek dueWeekday, DateOnly startsOn, DateTimeOffset now)
    {
        if (formDefinitionId == Guid.Empty) throw new ArgumentException("A check-in form is required.", nameof(formDefinitionId));
        if (!Intervals.Contains(intervalWeeks)) throw new ArgumentException("Check in every 1, 2 or 4 weeks.", nameof(intervalWeeks));
        if (!Enum.IsDefined(dueWeekday)) throw new ArgumentException("Unknown weekday.", nameof(dueWeekday));
        FormDefinitionId = formDefinitionId;
        IntervalWeeks = intervalWeeks;
        DueWeekday = dueWeekday;
        StartsOn = startsOn;
        UpdatedAt = now;
    }

    private CheckInCadence Cadence => new(IntervalWeeks, DueWeekday, StartsOn);

    /// <summary>The first due date: the first due weekday on or after the start.</summary>
    public DateOnly FirstDueOn => Cadence.FirstDueOn;

    /// <summary>The first due date on or after <paramref name="day"/>.</summary>
    public DateOnly DueOnOrAfter(DateOnly day) => Cadence.DueOnOrAfter(day);

    /// <summary>The latest due date on or before <paramref name="day"/>; null before the first.</summary>
    public DateOnly? DueOnOrBefore(DateOnly day) => Cadence.DueOnOrBefore(day);

    /// <summary>Every due date from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public IEnumerable<DateOnly> DueDatesBetween(DateOnly from, DateOnly to) => Cadence.DueDatesBetween(from, to);

    /// <summary>The due dates from <paramref name="from"/> to <paramref name="to"/> while this schedule is in force (P22).</summary>
    public IEnumerable<DateOnly> DueDatesInForce(DateOnly from, DateOnly to) =>
        Cadence.DueDatesBetween(InForceFrom is { } start && start > from ? start : from, to);
}

/// <summary>Check-in date arithmetic for a cadence: every <see cref="IntervalWeeks"/> weeks on a weekday from a start.</summary>
public readonly record struct CheckInCadence(int IntervalWeeks, DayOfWeek DueWeekday, DateOnly StartsOn)
{
    public DateOnly FirstDueOn => StartsOn.AddDays(((int)DueWeekday - (int)StartsOn.DayOfWeek + 7) % 7);

    public DateOnly DueOnOrAfter(DateOnly day)
    {
        var first = FirstDueOn;
        if (day <= first) return first;
        var step = IntervalWeeks * 7;
        var periods = (day.DayNumber - first.DayNumber + step - 1) / step;
        return first.AddDays(periods * step);
    }

    public DateOnly? DueOnOrBefore(DateOnly day)
    {
        var first = FirstDueOn;
        if (day < first) return null;
        var step = IntervalWeeks * 7;
        return first.AddDays((day.DayNumber - first.DayNumber) / step * step);
    }

    public IEnumerable<DateOnly> DueDatesBetween(DateOnly from, DateOnly to)
    {
        for (var due = DueOnOrAfter(from); due <= to; due = due.AddDays(IntervalWeeks * 7)) yield return due;
    }
}

/// <summary>
/// A client's earlier check-in schedule terms and the days they were in force (P22), left when the coach changes or
/// stops the schedule, or the client moves to another coach. Check-in adherence counts each past day under the terms
/// in force on it. Coaching data: it follows the client on transfer.
/// </summary>
public sealed class CheckInSchedulePeriod : Entity, ITenantOwned
{
    private CheckInSchedulePeriod() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid FormDefinitionId { get; private set; }
    public int IntervalWeeks { get; private set; }
    public DayOfWeek DueWeekday { get; private set; }
    public DateOnly StartsOn { get; private set; }

    /// <summary>The first and last day these terms were in force.</summary>
    public DateOnly InForceFrom { get; private set; }
    public DateOnly InForceTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static CheckInSchedulePeriod Record(CheckInSchedule schedule, DateOnly from, DateOnly to, DateTimeOffset now) => new()
    {
        CoachId = schedule.CoachId, ClientId = schedule.ClientId, FormDefinitionId = schedule.FormDefinitionId,
        IntervalWeeks = schedule.IntervalWeeks, DueWeekday = schedule.DueWeekday, StartsOn = schedule.StartsOn,
        InForceFrom = from, InForceTo = to, CreatedAt = now
    };

    /// <summary>The due dates from <paramref name="from"/> to <paramref name="to"/> while these terms were in force.</summary>
    public IEnumerable<DateOnly> DueDatesInForce(DateOnly from, DateOnly to) =>
        new CheckInCadence(IntervalWeeks, DueWeekday, StartsOn)
            .DueDatesBetween(from > InForceFrom ? from : InForceFrom, to < InForceTo ? to : InForceTo);
}

/// <summary>Where a submitted check-in stands with the coach (§29).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckInStatus>))]
public enum CheckInStatus
{
    AwaitingReview,
    Approved,
    ChangesRequested
}

/// <summary>
/// A submitted check-in for one due date (§28, §29): its submissions (a resubmission after "request changes" adds
/// one; all stay in the history), the coach's review, replies the client sees, and private notes the client never
/// sees. A check-in submitted after its due date is late but still completes its period.
/// </summary>
public sealed class CheckIn : Entity, ITenantOwned
{
    public const int MaximumTextLength = 4000;

    private readonly List<CheckInSubmission> _submissions = [];
    private readonly List<CheckInReply> _replies = [];

    private CheckIn() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid FormDefinitionId { get; private set; }
    public DateOnly DueOn { get; private set; }
    public CheckInStatus Status { get; private set; }

    /// <summary>The submission in force: the latest.</summary>
    public Guid SubmissionId { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>The first submission came after the due date.</summary>
    public bool IsLate { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>The coach's own notes; never shown to the client.</summary>
    public string? PrivateNotes { get; private set; }

    public IReadOnlyList<CheckInSubmission> Submissions => _submissions;
    public IReadOnlyList<CheckInReply> Replies => _replies;

    public static CheckIn Submit(
        Guid coachId, Guid clientId, Guid formDefinitionId, DateOnly dueOn, Guid submissionId, DateOnly submittedOn, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (formDefinitionId == Guid.Empty) throw new ArgumentException("A check-in form is required.", nameof(formDefinitionId));
        var checkIn = new CheckIn
        {
            CoachId = TenantKey.Required(coachId), ClientId = clientId, FormDefinitionId = formDefinitionId, DueOn = dueOn,
            IsLate = submittedOn > dueOn
        };
        checkIn.AddSubmission(submissionId, submittedOn > dueOn, now);
        return checkIn;
    }

    /// <summary>Resubmits after the coach requested changes; the check-in waits for review again.</summary>
    public void Resubmit(Guid submissionId, DateOnly submittedOn, DateTimeOffset now)
    {
        if (Status != CheckInStatus.ChangesRequested)
            throw new InvalidOperationException("Only a check-in with requested changes can be submitted again.");
        AddSubmission(submissionId, submittedOn > DueOn, now);
    }

    public void Approve(Guid reviewerUserId, string? message, DateTimeOffset now) =>
        Review(CheckInStatus.Approved, reviewerUserId, message, now);

    /// <summary>Asks the client to change and resubmit the check-in; the message says what to change.</summary>
    public void RequestChanges(Guid reviewerUserId, string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Say what the client should change.", nameof(message));
        Review(CheckInStatus.ChangesRequested, reviewerUserId, message, now);
    }

    /// <summary>A reply or feedback the client sees (§29).</summary>
    public CheckInReply Reply(Guid authorUserId, string text, DateTimeOffset now)
    {
        var reply = AddReply(authorUserId, text, now);
        Raise(new CheckInReviewed(Id, CoachId, ClientId, null));
        return reply;
    }

    public void ChangePrivateNotes(string? notes)
    {
        var value = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (value is { Length: > MaximumTextLength }) throw new ArgumentException($"Notes have at most {MaximumTextLength} characters.", nameof(notes));
        PrivateNotes = value;
    }

    private void Review(CheckInStatus status, Guid reviewerUserId, string? message, DateTimeOffset now)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        if (!string.IsNullOrWhiteSpace(message)) AddReply(reviewerUserId, message, now);
        Status = status;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = now;
        Raise(new CheckInReviewed(Id, CoachId, ClientId, status));
    }

    private CheckInReply AddReply(Guid authorUserId, string text, DateTimeOffset now)
    {
        var reply = CheckInReply.Create(Id, authorUserId, text, now);
        _replies.Add(reply);
        return reply;
    }

    private void AddSubmission(Guid submissionId, bool late, DateTimeOffset now)
    {
        if (submissionId == Guid.Empty) throw new ArgumentException("A submission is required.", nameof(submissionId));
        _submissions.Add(CheckInSubmission.Create(Id, submissionId, late, now));
        SubmissionId = submissionId;
        SubmittedAt = now;
        Status = CheckInStatus.AwaitingReview;
    }
}

/// <summary>One submission of a check-in (a form submission).</summary>
public sealed class CheckInSubmission : Entity
{
    private CheckInSubmission() { }

    public Guid CheckInId { get; private set; }
    public Guid FormSubmissionId { get; private set; }
    public bool IsLate { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    internal static CheckInSubmission Create(Guid checkInId, Guid submissionId, bool late, DateTimeOffset now) => new()
    {
        CheckInId = checkInId, FormSubmissionId = submissionId, IsLate = late, SubmittedAt = now
    };
}

/// <summary>A coach's reply or feedback on a check-in, visible to the client.</summary>
public sealed class CheckInReply : Entity
{
    private CheckInReply() { }

    public Guid CheckInId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    internal static CheckInReply Create(Guid checkInId, Guid authorUserId, string text, DateTimeOffset now)
    {
        if (authorUserId == Guid.Empty) throw new ArgumentException("An author is required.", nameof(authorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        var value = text.Trim();
        if (value.Length > CheckIn.MaximumTextLength)
            throw new ArgumentException($"A reply has at most {CheckIn.MaximumTextLength} characters.", nameof(text));
        return new CheckInReply { CheckInId = checkInId, AuthorUserId = authorUserId, Text = value, CreatedAt = now };
    }
}

/// <summary>The coach approved, asked for changes (<see cref="Outcome"/>) or replied (no outcome): the client is notified (§42).</summary>
public sealed record CheckInReviewed(Guid CheckInId, Guid CoachId, Guid ClientId, CheckInStatus? Outcome) : IDomainEvent;
