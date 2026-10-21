using System.Text.Json.Serialization;
using CoachHub.Domain.Calendar;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Common;

namespace CoachHub.Application.Calendar;

/// <summary>What a calendar entry is (§45, §46).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CalendarEntryKind>))]
public enum CalendarEntryKind
{
    /// <summary>A planned or logged workout; <see cref="CalendarEntry.Done"/> says whether it was done.</summary>
    WorkoutDay,
    RestDay,
    /// <summary>A check-in due date without a check-in yet.</summary>
    CheckInDue,
    /// <summary>A check-in the client sent, on its due date.</summary>
    CheckIn,
    ProgramStart,
    ProgramEnd,
    Event,
    FollowUp
}

/// <summary>
/// One entry on a calendar. <see cref="ReferenceId"/> points at what it describes (plan, check-in, event, workout);
/// <see cref="Done"/> is set for workouts (done or missed; null ahead) and follow-ups; <see cref="VisibleToClient"/>
/// for the coach's events and follow-ups.
/// </summary>
public sealed record CalendarEntry(
    DateOnly Date,
    CalendarEntryKind Kind,
    string Title,
    Guid? ClientId,
    string? ClientName,
    Guid? ReferenceId,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    bool? Done = null,
    string? Detail = null,
    PlanKind? PlanKind = null,
    bool? VisibleToClient = null);

public sealed record ClientCalendarResponse(
    Guid ClientId,
    string ClientName,
    DateOnly From,
    DateOnly To,
    [property: JsonConverter(typeof(DayOfWeekListConverter))] IReadOnlyList<DayOfWeek> TrainingWeekdays,
    IReadOnlyList<CalendarEntry> Entries);

public sealed record CoachCalendarResponse(DateOnly From, DateOnly To, IReadOnlyList<CalendarEntry> Entries);

public sealed record TrainingDaysInput([property: JsonConverter(typeof(DayOfWeekListConverter))] IReadOnlyList<DayOfWeek> Weekdays);

public sealed record CalendarEventInput(
    Guid? ClientId,
    CalendarEventKind Kind,
    string Title,
    DateOnly Date,
    string? Notes = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    bool VisibleToClient = false,
    bool IsDone = false);

public sealed record CalendarEventResponse(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    CalendarEventKind Kind,
    string Title,
    string? Notes,
    DateOnly Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool VisibleToClient,
    bool IsDone);

/// <summary>Weekdays travel as names ("Monday"), like every enum on the API.</summary>
public sealed class DayOfWeekListConverter : JsonConverter<IReadOnlyList<DayOfWeek>>
{
    public override IReadOnlyList<DayOfWeek> Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var names = System.Text.Json.JsonSerializer.Deserialize<string[]>(ref reader, options) ?? [];
        return names.Select(name => Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var day) && Enum.IsDefined(day)
            ? day
            : throw new System.Text.Json.JsonException($"Unknown weekday '{name}'.")).ToArray();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, IReadOnlyList<DayOfWeek> value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var day in value) writer.WriteStringValue(day.ToString());
        writer.WriteEndArray();
    }
}

/// <summary>A day of the workout rotation in force.</summary>
public sealed record RotationDay(int Order, string Name);

/// <summary>The workout version in force on a date, with its rotation.</summary>
public sealed record RotationInForce(Guid PlanId, string PlanName, IReadOnlyList<RotationDay> Days);

/// <summary>A workout the client logged.</summary>
public sealed record LoggedWorkout(Guid SessionId, DateOnly Date, string Name);

/// <summary>A plan's current version dates, for start and end markers.</summary>
public sealed record PlanDates(PlanKind Kind, Guid PlanId, string Name, Guid ClientId, string ClientName, DateOnly? StartDate, DateOnly? EndDate);

public sealed record ScheduledClient(Guid ClientId, string ClientName, CheckInSchedule Schedule);

public sealed record CheckInOnCalendar(Guid Id, Guid ClientId, string ClientName, DateOnly DueOn, CheckInStatus Status, bool IsLate);

public sealed record CalendarClient(Guid Id, Guid CoachId, string Name);

public interface ICalendarRepository
{
    Task<CalendarClient?> FindClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<TrainingDays?> FindTrainingDaysAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<(WorkoutTracking.WorkoutVersion Version, IReadOnlyList<RotationDay> Days)>> WorkoutVersionsAsync(Guid clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoggedWorkout>> CompletedWorkoutsAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    /// <summary>The check-in schedules of one client, or of every visible client.</summary>
    Task<IReadOnlyList<ScheduledClient>> SchedulesAsync(Guid? clientId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckInOnCalendar>> CheckInsAsync(Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    /// <summary>Client plans whose current version starts or ends in the range.</summary>
    Task<IReadOnlyList<PlanDates>> PlanDatesAsync(Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<(CalendarEvent Event, string? ClientName)>> EventsAsync(Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<CalendarEvent?> FindEventAsync(Guid id, CancellationToken cancellationToken);
    void Add(TrainingDays days);
    void Add(CalendarEvent calendarEvent);
    void Remove(CalendarEvent calendarEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
