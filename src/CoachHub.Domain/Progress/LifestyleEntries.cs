using CoachHub.Domain.Common;

namespace CoachHub.Domain.Progress;

/// <summary>
/// A night's sleep (§31), dated by the morning it ended: hours, quality (1–5), bedtime and wake time. Hours follow
/// from bedtime and wake time when not entered.
/// </summary>
public sealed class SleepEntry : ProgressEntry
{
    public const int MinimumQuality = 1;
    public const int MaximumQuality = 5;

    private SleepEntry() { }

    public decimal Hours { get; private set; }

    /// <summary>1 (very poor) to 5 (very good).</summary>
    public int? Quality { get; private set; }
    public TimeOnly? Bedtime { get; private set; }
    public TimeOnly? WakeTime { get; private set; }

    public static SleepEntry Create(
        Guid coachId, Guid clientId, DateOnly date, decimal? hours, int? quality, TimeOnly? bedtime, TimeOnly? wakeTime,
        string? notes, DateTimeOffset now, EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        var entry = new SleepEntry();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.Apply(hours, quality, bedtime, wakeTime);
        return entry;
    }

    public void Change(decimal? hours, int? quality, TimeOnly? bedtime, TimeOnly? wakeTime, string? notes, DateTimeOffset now)
    {
        Apply(hours, quality, bedtime, wakeTime);
        SetNotes(notes, now);
    }

    /// <summary>The time between bedtime and waking, across midnight, in hours rounded to two decimals.</summary>
    public static decimal HoursBetween(TimeOnly bedtime, TimeOnly wakeTime)
    {
        var minutes = (wakeTime.ToTimeSpan() - bedtime.ToTimeSpan()).TotalMinutes;
        if (minutes <= 0) minutes += 24 * 60;
        return decimal.Round((decimal)minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private void Apply(decimal? hours, int? quality, TimeOnly? bedtime, TimeOnly? wakeTime)
    {
        if (bedtime.HasValue != wakeTime.HasValue) throw new ArgumentException("Enter both bedtime and wake time, or neither.", "wakeTime");
        var value = hours ?? (bedtime is { } bed && wakeTime is { } wake ? HoursBetween(bed, wake)
            : throw new ArgumentException("Enter the hours slept, or bedtime and wake time.", "hours"));
        Hours = Measure(value, 0m, 24m, 2, "hours", "hours");
        Quality = quality is { } q ? Count(q, MinimumQuality, MaximumQuality, "quality") : null;
        Bedtime = bedtime;
        WakeTime = wakeTime;
    }
}

/// <summary>The day's step count (§32): one per day.</summary>
public sealed class StepsEntry : ProgressEntry
{
    public const int MaximumSteps = 200_000;

    private StepsEntry() { }

    public int Steps { get; private set; }

    public static StepsEntry Create(
        Guid coachId, Guid clientId, DateOnly date, int steps, string? notes, DateTimeOffset now,
        EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        var entry = new StepsEntry();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.Steps = Count(steps, 0, MaximumSteps, "steps");
        return entry;
    }

    public void Change(int steps, string? notes, DateTimeOffset now)
    {
        Steps = Count(steps, 0, MaximumSteps, "steps");
        SetNotes(notes, now);
    }
}

/// <summary>Water drunk (§33): each entry is one intake in ml, and a day's consumption is their sum.</summary>
public sealed class WaterEntry : ProgressEntry
{
    public const int MaximumMl = 10_000;

    private WaterEntry() { }

    public int AmountMl { get; private set; }

    public static WaterEntry Create(
        Guid coachId, Guid clientId, DateOnly date, int amountMl, string? notes, DateTimeOffset now,
        EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        var entry = new WaterEntry();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.AmountMl = Count(amountMl, 1, MaximumMl, "amountMl");
        return entry;
    }
}

/// <summary>Daily targets for steps, water and sleep (§32, §33, §31), set by the coach.</summary>
public sealed record LifestyleTargetValues(int? Steps, int? WaterMl, decimal? SleepHours)
{
    public static readonly LifestyleTargetValues None = new(null, null, null);

    public LifestyleTargetValues Validate()
    {
        if (Steps is < 100 or > 100_000) throw new ArgumentException("A steps target is 100–100,000.", "steps");
        if (WaterMl is < 250 or > 10_000) throw new ArgumentException("A water target is 250–10,000 ml.", "waterMl");
        if (SleepHours is { } sleep && (sleep < 3 || sleep > 14 || decimal.Round(sleep, 2) != sleep))
            throw new ArgumentException("A sleep target is 3–14 hours.", "sleepHours");
        return this;
    }

    /// <summary>Each value set here, otherwise the fallback's (the coach's defaults).</summary>
    public LifestyleTargetValues Or(LifestyleTargetValues fallback) =>
        new(Steps ?? fallback.Steps, WaterMl ?? fallback.WaterMl, SleepHours ?? fallback.SleepHours);
}

/// <summary>A client's daily lifestyle targets; written by the coach, one row per client.</summary>
public sealed class LifestyleTargets : Entity, ITenantOwned
{
    private LifestyleTargets() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public int? StepsTarget { get; private set; }
    public int? WaterTargetMl { get; private set; }
    public decimal? SleepTargetHours { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public LifestyleTargetValues Values => new(StepsTarget, WaterTargetMl, SleepTargetHours);

    public static LifestyleTargets Create(Guid coachId, Guid clientId, LifestyleTargetValues values, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        var targets = new LifestyleTargets { CoachId = TenantKey.Required(coachId), ClientId = clientId };
        targets.Change(values, now);
        return targets;
    }

    public void Change(LifestyleTargetValues values, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        values.Validate();
        (StepsTarget, WaterTargetMl, SleepTargetHours) = (values.Steps, values.WaterMl, values.SleepHours);
        UpdatedAt = now;
    }
}
