using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Calendar;

/// <summary>The weekdays a client trains, as a set (exposed by the API as a list of day names).</summary>
[Flags]
public enum TrainingWeekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
}

public static class TrainingWeekdaySet
{
    public static TrainingWeekdays Flag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => TrainingWeekdays.Monday,
        DayOfWeek.Tuesday => TrainingWeekdays.Tuesday,
        DayOfWeek.Wednesday => TrainingWeekdays.Wednesday,
        DayOfWeek.Thursday => TrainingWeekdays.Thursday,
        DayOfWeek.Friday => TrainingWeekdays.Friday,
        DayOfWeek.Saturday => TrainingWeekdays.Saturday,
        DayOfWeek.Sunday => TrainingWeekdays.Sunday,
        _ => throw new ArgumentException("Unknown weekday.", nameof(day))
    };

    public static TrainingWeekdays Of(IEnumerable<DayOfWeek> days) =>
        days.Aggregate(TrainingWeekdays.None, (set, day) => set | Flag(day));

    /// <summary>The days in the set, Monday first.</summary>
    public static IReadOnlyList<DayOfWeek> Days(TrainingWeekdays set) =>
        new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Where(day => set.HasFlag(Flag(day))).ToArray();
}

/// <summary>
/// The weekdays the coach set for a client's training (§45, owner decision 2026-10-22). The workout rotation fills
/// them in order each week; the other days are rest days.
/// </summary>
public sealed class TrainingDays : Entity, ITenantOwned
{
    private TrainingDays() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public TrainingWeekdays Weekdays { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TrainingDays Create(Guid coachId, Guid clientId, TrainingWeekdays weekdays, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        var days = new TrainingDays { CoachId = TenantKey.Required(coachId), ClientId = clientId };
        days.Change(weekdays, now);
        return days;
    }

    public void Change(TrainingWeekdays weekdays, DateTimeOffset now)
    {
        if ((weekdays & ~TrainingWeekdays.All) != 0) throw new ArgumentException("Unknown weekday.", "weekdays");
        if (weekdays == TrainingWeekdays.None) throw new ArgumentException("Choose at least one training day.", "weekdays");
        Weekdays = weekdays;
        UpdatedAt = now;
    }
}

/// <summary>A manual calendar entry (§45 important events, §46 client events and follow-ups).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CalendarEventKind>))]
public enum CalendarEventKind
{
    Event,
    FollowUp
}

/// <summary>
/// An event or follow-up the coach adds (owner decision 2026-10-22: coaches only), for one client or for
/// themselves. An event for a client can be shown on the client's calendar; follow-ups are always the coach's own.
/// </summary>
public sealed class CalendarEvent : Entity, ITenantOwned
{
    public const int MaximumTitleLength = 200;
    public const int MaximumNotesLength = 1000;

    private CalendarEvent() { }

    public Guid CoachId { get; private set; }
    public Guid? ClientId { get; private set; }
    public CalendarEventKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public bool VisibleToClient { get; private set; }
    /// <summary>A follow-up that was done.</summary>
    public bool IsDone { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CalendarEvent Create(
        Guid coachId, Guid? clientId, CalendarEventKind kind, string title, string? notes, DateOnly date,
        TimeOnly? startTime, TimeOnly? endTime, bool visibleToClient, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier cannot be empty.", nameof(clientId));
        var calendarEvent = new CalendarEvent { CoachId = TenantKey.Required(coachId), ClientId = clientId, CreatedAt = now };
        calendarEvent.Change(kind, title, notes, date, startTime, endTime, visibleToClient, false, now);
        return calendarEvent;
    }

    public void Change(
        CalendarEventKind kind, string title, string? notes, DateOnly date, TimeOnly? startTime, TimeOnly? endTime,
        bool visibleToClient, bool isDone, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Unknown kind.", nameof(kind));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required.", nameof(title));
        var name = title.Trim();
        if (name.Length > MaximumTitleLength) throw new ArgumentException($"A title has at most {MaximumTitleLength} characters.", nameof(title));
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (text is { Length: > MaximumNotesLength }) throw new ArgumentException($"Notes have at most {MaximumNotesLength} characters.", nameof(notes));
        if (endTime is not null && startTime is null) throw new ArgumentException("An end time needs a start time.", nameof(endTime));
        if (endTime is { } end && startTime is { } start && end <= start)
            throw new ArgumentException("The end time is after the start time.", nameof(endTime));
        if (visibleToClient && ClientId is null) throw new ArgumentException("Only an event for a client can be shown to the client.", nameof(visibleToClient));
        if (visibleToClient && kind == CalendarEventKind.FollowUp)
            throw new ArgumentException("Follow-ups are the coach's own and never shown to the client.", nameof(visibleToClient));
        Kind = kind;
        Title = name;
        Notes = text;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        VisibleToClient = visibleToClient;
        IsDone = isDone;
        UpdatedAt = now;
    }
}
