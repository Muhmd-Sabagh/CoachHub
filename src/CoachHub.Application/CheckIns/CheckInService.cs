using CoachHub.Application.Assessments;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.CheckIns;

/// <summary>
/// The check-in workflow (§28–§30): the coach schedules a check-in form per client; the client submits it on or
/// around the due date; the coach reviews, replies, approves or requests changes (the client then resubmits). The
/// client's next due date and whether a check-in awaits review drive the status (§39).
/// </summary>
public sealed class CheckInService(
    ICheckInRepository repository, IFormRepository forms, ICurrentUser currentUser, TimeProvider timeProvider)
{
    /// <summary>A check-in opens this many days before its due date.</summary>
    public const int OpensDaysBefore = 3;

    public async Task<CheckInScheduleResponse?> GetScheduleAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await RequiredClientAsync(clientId, cancellationToken);
        var schedule = await repository.FindScheduleAsync(clientId, cancellationToken);
        return schedule is null ? null : await MapAsync(client, schedule, cancellationToken);
    }

    /// <summary>Sets the client's schedule (§28). The next due date is the first on or after today not yet checked in.</summary>
    public async Task<CheckInScheduleResponse> SetScheduleAsync(Guid clientId, CheckInScheduleInput input, CancellationToken cancellationToken)
    {
        var client = await RequiredClientAsync(clientId, cancellationToken);
        var form = await repository.FindFormAsync(input.FormDefinitionId, cancellationToken);
        if (form is null || form.CoachId != client.CoachId || form.FormType != AssessmentFormType.CheckIn || form.IsArchived)
            throw Validation("formDefinitionId", "Choose one of your active check-in forms.");
        if (!await repository.HasPublishedVersionAsync(form.Id, cancellationToken))
            throw Validation("formDefinitionId", "Publish the check-in form first.");
        var now = timeProvider.GetUtcNow();
        var today = Today();
        var schedule = await repository.FindScheduleAsync(clientId, cancellationToken);
        DomainValidation.Run(() =>
        {
            if (schedule is null)
            {
                schedule = CheckInSchedule.Create(client.CoachId, client.Id, form.Id, input.IntervalWeeks, input.DueWeekday, input.StartsOn ?? today, now);
                repository.Add(schedule);
            }
            else schedule.Change(form.Id, input.IntervalWeeks, input.DueWeekday, input.StartsOn ?? schedule.StartsOn, now);
        }, "schedule");
        var history = await repository.ListForClientAsync(clientId, cancellationToken);
        var next = schedule!.DueOnOrAfter(today > schedule.StartsOn ? today : schedule.StartsOn);
        while (history.Any(x => x.DueOn == next)) next = next.AddDays(schedule.IntervalWeeks * 7);
        client.SetCheckInState(next, history.Any(x => x.Status == CheckInStatus.AwaitingReview));
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(client, schedule, cancellationToken);
    }

    /// <summary>Stops the client's check-ins; their history stays.</summary>
    public async Task DeleteScheduleAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await RequiredClientAsync(clientId, cancellationToken);
        var schedule = await repository.FindScheduleAsync(clientId, cancellationToken) ?? throw new NotFoundException("Check-in schedule", clientId);
        repository.Remove(schedule);
        client.SetCheckInState(null, client.CheckInAwaitingReview);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Whether the client may fill in a check-in form now (for the client's list of forms).</summary>
    public async Task<bool> IsOpenAsync(Client client, Guid definitionId, CancellationToken cancellationToken)
    {
        try
        {
            await TargetAsync(client, definitionId, Today(), cancellationToken);
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }

    /// <summary>
    /// Where a check-in submission goes, checked before the submission is stored: a resubmission after "request
    /// changes"; otherwise the scheduled form, open from <see cref="OpensDaysBefore"/> days before the due date. A late
    /// check-in counts for the latest due date that has passed; earlier missed ones stay missed.
    /// </summary>
    public async Task<CheckInTarget> TargetAsync(Client client, Guid definitionId, DateOnly submittedOn, CancellationToken cancellationToken)
    {
        var history = await repository.ListForClientAsync(client.Id, cancellationToken);
        var reopened = history.Where(x => x.Status == CheckInStatus.ChangesRequested && x.FormDefinitionId == definitionId)
            .OrderByDescending(x => x.DueOn).FirstOrDefault();
        if (reopened is not null) return new CheckInTarget(reopened, reopened.DueOn, null);

        var schedule = await repository.FindScheduleAsync(client.Id, cancellationToken);
        if (schedule is null || schedule.FormDefinitionId != definitionId || client.CheckInDueOn is not { } due)
            throw new ConflictException("This check-in is not scheduled for you.");
        if (submittedOn < due.AddDays(-OpensDaysBefore))
            throw new ConflictException($"The next check-in opens on {due.AddDays(-OpensDaysBefore):yyyy-MM-dd}.");
        var dueOn = submittedOn > due ? schedule.DueOnOrBefore(submittedOn) ?? due : due;
        return new CheckInTarget(null, dueOn, schedule);
    }

    /// <summary>Records a stored check-in submission against its target, and moves the next due date on.</summary>
    public async Task RecordAsync(
        Client client, CheckInTarget target, Guid definitionId, Guid submissionId, DateOnly submittedOn, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (target.Existing is { } existing)
        {
            var tracked = await repository.FindAsync(existing.Id, true, cancellationToken) ?? throw new NotFoundException("Check-in", existing.Id);
            tracked.Resubmit(submissionId, submittedOn, now);
        }
        else
        {
            repository.Add(CheckIn.Submit(client.CoachId, client.Id, definitionId, target.DueOn, submissionId, submittedOn, now));
            client.SetCheckInState(target.DueOn.AddDays(target.Schedule!.IntervalWeeks * 7), true);
        }
        client.SetCheckInState(client.CheckInDueOn, true);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Submitted check-ins (§29 completed and history), newest first.</summary>
    public async Task<PagedResult<CheckInSummary>> ListAsync(CheckInQuery query, CancellationToken cancellationToken)
    {
        if (query.ClientId is { } clientId) await RequiredClientAsync(clientId, cancellationToken);
        var page = await repository.PageAsync(query.Normalize(), cancellationToken);
        var clients = await repository.FindClientNamesAsync(page.Items.Select(x => x.ClientId).Distinct().ToArray(), cancellationToken);
        var formNames = await repository.FindFormNamesAsync(page.Items.Select(x => x.FormDefinitionId).Distinct().ToArray(), cancellationToken);
        return new PagedResult<CheckInSummary>(page.Items.Select(x => Summary(x, clients, formNames)).ToArray(),
            page.PageNumber, page.PageSize, page.TotalCount);
    }

    /// <summary>Scheduled clients whose check-in is pending (open or upcoming) or overdue (§29), the most urgent first.</summary>
    public async Task<PagedResult<CheckInDueResponse>> DueAsync(CheckInDueQuery query, CancellationToken cancellationToken)
    {
        var today = Today();
        var page = await repository.PageDueAsync(query.Normalize(), today, cancellationToken);
        return new PagedResult<CheckInDueResponse>(page.Items.Select(client =>
        {
            var due = client.CheckInDueOn!.Value;
            return new CheckInDueResponse(client.Id, client.Name, due, State(due, today), Math.Max(0, today.DayNumber - due.DayNumber));
        }).ToArray(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<CheckInDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var checkIn = await RequiredAsync(id, false, cancellationToken);
        return await DetailAsync(checkIn, cancellationToken);
    }

    /// <summary>A reply or feedback the client sees (§29 Reply, Send feedback).</summary>
    public Task<CheckInDetail> ReplyAsync(Guid id, CheckInReplyInput input, CancellationToken cancellationToken) =>
        ReviewAsync(id, checkIn => checkIn.Reply(ReviewerId(), input.Text, timeProvider.GetUtcNow()), cancellationToken);

    /// <summary>The coach's private notes (§29 Add notes); never shown to the client.</summary>
    public Task<CheckInDetail> ChangeNotesAsync(Guid id, CheckInNotesInput input, CancellationToken cancellationToken) =>
        ReviewAsync(id, checkIn => checkIn.ChangePrivateNotes(input.Notes), cancellationToken);

    public Task<CheckInDetail> ApproveAsync(Guid id, CheckInReviewInput input, CancellationToken cancellationToken) =>
        ReviewAsync(id, checkIn => checkIn.Approve(ReviewerId(), input.Message, timeProvider.GetUtcNow()), cancellationToken);

    /// <summary>Asks the client to change and resubmit the check-in (§29 Request changes).</summary>
    public Task<CheckInDetail> RequestChangesAsync(Guid id, CheckInReviewInput input, CancellationToken cancellationToken) =>
        ReviewAsync(id, checkIn => checkIn.RequestChanges(ReviewerId(), input.Message ?? string.Empty, timeProvider.GetUtcNow()), cancellationToken);

    /// <summary>The client's own check-ins: schedule, a check-in to resubmit, and history without private notes.</summary>
    public async Task<MyCheckInsResponse> MineAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await RequiredClientAsync(clientId, cancellationToken);
        var schedule = await repository.FindScheduleAsync(clientId, cancellationToken);
        var history = (await repository.ListForClientAsync(clientId, cancellationToken)).OrderByDescending(x => x.DueOn).ToArray();
        var formNames = await repository.FindFormNamesAsync(history.Select(x => x.FormDefinitionId).Distinct().ToArray(), cancellationToken);
        var authors = await repository.FindUserNamesAsync(history.SelectMany(x => x.Replies).Select(x => x.AuthorUserId).Distinct().ToArray(), cancellationToken);
        return new MyCheckInsResponse(
            schedule is null ? null : await MapAsync(client, schedule, cancellationToken),
            history.FirstOrDefault(x => x.Status == CheckInStatus.ChangesRequested)?.Id,
            history.Select(x => Mine(x, formNames, authors, [])).ToArray());
    }

    public async Task<MyCheckInResponse> MyCheckInAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        await RequiredClientAsync(clientId, cancellationToken);
        var checkIn = await repository.FindAsync(id, false, cancellationToken);
        if (checkIn is null || checkIn.ClientId != clientId) throw new NotFoundException("Check-in", id);
        var formNames = await repository.FindFormNamesAsync([checkIn.FormDefinitionId], cancellationToken);
        var authors = await repository.FindUserNamesAsync(checkIn.Replies.Select(x => x.AuthorUserId).Distinct().ToArray(), cancellationToken);
        var submission = await forms.GetSubmissionAsync(checkIn.SubmissionId, cancellationToken);
        return Mine(checkIn, formNames, authors, submission?.Answers ?? []);
    }

    /// <summary>
    /// Check-in adherence (§30) from <paramref name="from"/> to <paramref name="to"/>: the schedule's due dates up to
    /// today, and how many have a check-in (late ones count). Null without a schedule or due dates.
    /// </summary>
    public async Task<CheckInAdherence> AdherenceAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var schedule = await repository.FindScheduleAsync(clientId, cancellationToken);
        var history = await repository.ListForClientAsync(clientId, cancellationToken);
        var last = to < Today() ? to : Today();
        if (schedule is null || last < from) return new CheckInAdherence(0, 0, 0, null);
        var due = schedule.DueDatesBetween(from, last).ToArray();
        var done = history.Where(x => due.Contains(x.DueOn)).ToArray();
        return new CheckInAdherence(due.Length, done.Length, done.Count(x => x.IsLate),
            due.Length == 0 ? null : FoodTracking.NutritionAdherence.Share(done.Length, due.Length));
    }

    private async Task<CheckInDetail> ReviewAsync(Guid id, Action<CheckIn> change, CancellationToken cancellationToken)
    {
        var checkIn = await RequiredAsync(id, true, cancellationToken);
        var client = await RequiredClientAsync(checkIn.ClientId, cancellationToken);
        DomainValidation.Run(() => change(checkIn), "checkIn");
        var history = await repository.ListForClientAsync(client.Id, cancellationToken);
        client.SetCheckInState(client.CheckInDueOn,
            history.Where(x => x.Id != checkIn.Id).Any(x => x.Status == CheckInStatus.AwaitingReview) || checkIn.Status == CheckInStatus.AwaitingReview);
        await repository.SaveChangesAsync(cancellationToken);
        return await DetailAsync(checkIn, cancellationToken);
    }

    private async Task<CheckInDetail> DetailAsync(CheckIn checkIn, CancellationToken cancellationToken)
    {
        var clients = await repository.FindClientNamesAsync([checkIn.ClientId], cancellationToken);
        var formNames = await repository.FindFormNamesAsync([checkIn.FormDefinitionId], cancellationToken);
        var authors = await repository.FindUserNamesAsync(checkIn.Replies.Select(x => x.AuthorUserId).Distinct().ToArray(), cancellationToken);
        var submission = await forms.GetSubmissionAsync(checkIn.SubmissionId, cancellationToken);
        return new CheckInDetail(Summary(checkIn, clients, formNames), submission?.Answers ?? [],
            Submissions(checkIn), Replies(checkIn, authors), checkIn.PrivateNotes, checkIn.ReviewedAt);
    }

    private async Task<CheckInScheduleResponse> MapAsync(Client client, CheckInSchedule schedule, CancellationToken cancellationToken)
    {
        var names = await repository.FindFormNamesAsync([schedule.FormDefinitionId], cancellationToken);
        var due = client.CheckInDueOn;
        return new CheckInScheduleResponse(client.Id, schedule.FormDefinitionId, names.GetValueOrDefault(schedule.FormDefinitionId) ?? "Check-in",
            schedule.IntervalWeeks, schedule.DueWeekday, schedule.StartsOn, due, due?.AddDays(-OpensDaysBefore),
            due is { } d ? State(d, Today()) : null);
    }

    private static CheckInDueState State(DateOnly due, DateOnly today) =>
        due < today ? CheckInDueState.Overdue : today >= due.AddDays(-OpensDaysBefore) ? CheckInDueState.Pending : CheckInDueState.Upcoming;

    private static CheckInSummary Summary(CheckIn x, IReadOnlyDictionary<Guid, string> clients, IReadOnlyDictionary<Guid, string> forms) => new(
        x.Id, x.ClientId, clients.GetValueOrDefault(x.ClientId) ?? "Client", x.FormDefinitionId, forms.GetValueOrDefault(x.FormDefinitionId) ?? "Check-in",
        x.DueOn, x.SubmittedAt, x.IsLate, x.Status, x.Submissions.Count, x.Replies.Count);

    private static MyCheckInResponse Mine(
        CheckIn x, IReadOnlyDictionary<Guid, string> forms, IReadOnlyDictionary<Guid, string> authors, IReadOnlyList<AssessmentAnswerResponse> answers) => new(
        x.Id, x.FormDefinitionId, forms.GetValueOrDefault(x.FormDefinitionId) ?? "Check-in", x.DueOn, x.SubmittedAt, x.IsLate, x.Status,
        Submissions(x), Replies(x, authors), answers);

    private static CheckInSubmissionResponse[] Submissions(CheckIn x) =>
        x.Submissions.OrderBy(s => s.SubmittedAt).Select(s => new CheckInSubmissionResponse(s.FormSubmissionId, s.SubmittedAt, s.IsLate)).ToArray();

    private static CheckInReplyResponse[] Replies(CheckIn x, IReadOnlyDictionary<Guid, string> authors) =>
        x.Replies.OrderBy(r => r.CreatedAt).Select(r => new CheckInReplyResponse(r.Id, r.Text, authors.GetValueOrDefault(r.AuthorUserId), r.CreatedAt)).ToArray();

    private Guid ReviewerId() => currentUser.UserId ?? throw new ForbiddenException("A signed-in reviewer is required.");

    private async Task<Client> RequiredClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private async Task<CheckIn> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, tracking, cancellationToken) ?? throw new NotFoundException("Check-in", id);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
}
