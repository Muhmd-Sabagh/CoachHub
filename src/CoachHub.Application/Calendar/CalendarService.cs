using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Calendar;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Common;

namespace CoachHub.Application.Calendar;

/// <summary>
/// The client calendar (§45) and the coach calendar (§46). Entries are derived (workout and rest days from the
/// rotation on the training weekdays, check-in due dates, plan start and end dates) plus the coach's events and
/// follow-ups. The coach edits the training weekdays and the events; the client reads their calendar, which never
/// shows follow-ups or events not shared with them.
/// </summary>
public sealed class CalendarService(ICalendarRepository repository, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public const int MaximumRangeDays = 62;

    public async Task<ClientCalendarResponse> ClientAsync(Guid clientId, DateOnly from, DateOnly to, bool asClient, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        var client = await ClientOfAsync(clientId, cancellationToken);
        var today = Today();
        var training = await repository.FindTrainingDaysAsync(clientId, cancellationToken);
        var entries = new List<CalendarEntry>();

        // Workout and rest days, and the workouts actually logged.
        var versions = await repository.WorkoutVersionsAsync(clientId, cancellationToken);
        var logged = (await repository.CompletedWorkoutsAsync(clientId, from, to, cancellationToken)).ToLookup(x => x.Date);
        var planned = TrainingCalendar.Plan(from, to, training?.Weekdays ?? TrainingWeekdays.None, date =>
        {
            var version = WorkoutSchedule.InForce(versions.Select(x => x.Version), date);
            if (version is null) return null;
            var days = versions.First(x => x.Version.Revision.Id == version.Revision.Id).Days;
            return new RotationInForce(version.Plan.Id, version.Plan.NameEn, days);
        }).ToDictionary(x => x.Date);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var done = logged[date].ToArray();
            foreach (var workout in done)
                entries.Add(new CalendarEntry(date, CalendarEntryKind.WorkoutDay, workout.Name, clientId, client.Name, workout.SessionId, Done: true));
            if (!planned.TryGetValue(date, out var plan)) continue;
            if (plan.Workout is { } day)
            {
                if (done.Length == 0)
                    entries.Add(new CalendarEntry(date, CalendarEntryKind.WorkoutDay, day.Name, clientId, client.Name, plan.PlanId,
                        Done: date < today ? false : null, PlanKind: PlanKind.Workout));
            }
            else if (done.Length == 0)
                entries.Add(new CalendarEntry(date, CalendarEntryKind.RestDay, "Rest day", clientId, client.Name, plan.PlanId, PlanKind: PlanKind.Workout));
        }

        entries.AddRange(await CheckInEntriesAsync(clientId, from, to, cancellationToken));
        entries.AddRange(ProgramEntries(await repository.PlanDatesAsync(clientId, from, to, cancellationToken), from, to));
        foreach (var (item, name) in await repository.EventsAsync(clientId, from, to, cancellationToken))
        {
            if (asClient && !item.VisibleToClient) continue;
            entries.Add(EventEntry(item, name));
        }
        return new ClientCalendarResponse(client.Id, client.Name, from, to,
            training is null ? [] : TrainingWeekdaySet.Days(training.Weekdays), Order(entries));
    }

    /// <summary>The coach's calendar across clients (§46), optionally for one client.</summary>
    public async Task<CoachCalendarResponse> CoachAsync(DateOnly from, DateOnly to, Guid? clientId, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        if (clientId is { } id) await ClientOfAsync(id, cancellationToken);
        var entries = new List<CalendarEntry>();
        entries.AddRange(await CheckInEntriesAsync(clientId, from, to, cancellationToken));
        entries.AddRange(ProgramEntries(await repository.PlanDatesAsync(clientId, from, to, cancellationToken), from, to));
        foreach (var (item, name) in await repository.EventsAsync(clientId, from, to, cancellationToken)) entries.Add(EventEntry(item, name));
        return new CoachCalendarResponse(from, to, Order(entries));
    }

    public async Task<ClientCalendarResponse> SetTrainingDaysAsync(Guid clientId, TrainingDaysInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var client = await ClientOfAsync(clientId, cancellationToken);
        var weekdays = TrainingWeekdaySet.Of(input.Weekdays ?? []);
        var now = timeProvider.GetUtcNow();
        var existing = await repository.FindTrainingDaysAsync(clientId, cancellationToken);
        if (existing is null) repository.Add(DomainValidation.Run(() => TrainingDays.Create(client.CoachId, clientId, weekdays, now), "weekdays"));
        else DomainValidation.Run(() => existing.Change(weekdays, now), "weekdays");
        await repository.SaveChangesAsync(cancellationToken);
        var today = Today();
        return await ClientAsync(clientId, today, today.AddDays(6), false, cancellationToken);
    }

    public async Task<CalendarEventResponse> CreateEventAsync(CalendarEventInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        Guid coachId;
        string? clientName = null;
        if (input.ClientId is { } clientId)
        {
            var client = await ClientOfAsync(clientId, cancellationToken);
            (coachId, clientName) = (client.CoachId, client.Name);
        }
        else
        {
            // An event of the coach's own; administrators always name a client.
            coachId = currentUser.CoachId ?? throw Validation("clientId", "Choose the client the event is for.");
        }
        var item = DomainValidation.Run(() => CalendarEvent.Create(coachId, input.ClientId, input.Kind, input.Title, input.Notes, input.Date,
            input.StartTime, input.EndTime, input.VisibleToClient, timeProvider.GetUtcNow()), "title");
        if (input.IsDone) item.Change(item.Kind, item.Title, item.Notes, item.Date, item.StartTime, item.EndTime, item.VisibleToClient, true, timeProvider.GetUtcNow());
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(item, clientName);
    }

    /// <summary>Changes an event; its client stays (delete and add it again to move it to another client).</summary>
    public async Task<CalendarEventResponse> UpdateEventAsync(Guid id, CalendarEventInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var item = await repository.FindEventAsync(id, cancellationToken) ?? throw new NotFoundException("Calendar event", id);
        if (input.ClientId != item.ClientId) throw Validation("clientId", "An event keeps its client.");
        DomainValidation.Run(() => item.Change(input.Kind, input.Title, input.Notes, input.Date, input.StartTime, input.EndTime,
            input.VisibleToClient, input.IsDone, timeProvider.GetUtcNow()), "title");
        await repository.SaveChangesAsync(cancellationToken);
        var name = item.ClientId is { } clientId ? (await repository.FindClientAsync(clientId, cancellationToken))?.Name : null;
        return Map(item, name);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.FindEventAsync(id, cancellationToken) ?? throw new NotFoundException("Calendar event", id);
        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IEnumerable<CalendarEntry>> CheckInEntriesAsync(Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var entries = new List<CalendarEntry>();
        var checkIns = await repository.CheckInsAsync(clientId, from, to, cancellationToken);
        var sent = checkIns.Select(x => (x.ClientId, x.DueOn)).ToHashSet();
        foreach (var checkIn in checkIns)
        {
            entries.Add(new CalendarEntry(checkIn.DueOn, CalendarEntryKind.CheckIn, "Check-in", checkIn.ClientId, checkIn.ClientName, checkIn.Id,
                Done: checkIn.Status == CheckInStatus.Approved, Detail: checkIn.Status + (checkIn.IsLate ? " · late" : "")));
        }
        foreach (var scheduled in await repository.SchedulesAsync(clientId, cancellationToken))
        {
            foreach (var due in scheduled.Schedule.DueDatesBetween(from, to))
            {
                if (sent.Contains((scheduled.ClientId, due))) continue;
                entries.Add(new CalendarEntry(due, CalendarEntryKind.CheckInDue, "Check-in due", scheduled.ClientId, scheduled.ClientName,
                    scheduled.Schedule.Id, Done: due < Today() ? false : null));
            }
        }
        return entries;
    }

    private static IEnumerable<CalendarEntry> ProgramEntries(IEnumerable<PlanDates> plans, DateOnly from, DateOnly to)
    {
        foreach (var plan in plans)
        {
            var noun = plan.Kind == PlanKind.Diet ? "Diet plan" : "Workout plan";
            if (plan.StartDate is { } start && start >= from && start <= to)
                yield return new CalendarEntry(start, CalendarEntryKind.ProgramStart, $"{noun} starts: {plan.Name}", plan.ClientId, plan.ClientName, plan.PlanId, PlanKind: plan.Kind);
            if (plan.EndDate is { } end && end >= from && end <= to)
                yield return new CalendarEntry(end, CalendarEntryKind.ProgramEnd, $"{noun} ends: {plan.Name}", plan.ClientId, plan.ClientName, plan.PlanId, PlanKind: plan.Kind);
        }
    }

    private static CalendarEntry EventEntry(CalendarEvent item, string? clientName) =>
        new(item.Date, item.Kind == CalendarEventKind.FollowUp ? CalendarEntryKind.FollowUp : CalendarEntryKind.Event, item.Title,
            item.ClientId, clientName, item.Id, item.StartTime, item.EndTime,
            item.Kind == CalendarEventKind.FollowUp ? item.IsDone : null, item.Notes, VisibleToClient: item.VisibleToClient);

    private static IReadOnlyList<CalendarEntry> Order(IEnumerable<CalendarEntry> entries) =>
        entries.OrderBy(x => x.Date).ThenBy(x => x.StartTime ?? TimeOnly.MinValue).ThenBy(x => x.Kind).ThenBy(x => x.ClientName).ToArray();

    internal static CalendarEventResponse Map(CalendarEvent item, string? clientName) => new(
        item.Id, item.ClientId, clientName, item.Kind, item.Title, item.Notes, item.Date, item.StartTime, item.EndTime, item.VisibleToClient, item.IsDone);

    private async Task<CalendarClient> ClientOfAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private static void EnsureRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw Validation("to", "The end date cannot be before the start date.");
        if (to.DayNumber - from.DayNumber >= MaximumRangeDays) throw Validation("to", $"A calendar covers at most {MaximumRangeDays} days.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private static ValidationException Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
