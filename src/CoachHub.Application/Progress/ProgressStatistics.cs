using CoachHub.Application.FoodTracking;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Progress;

public sealed record SeriesPoint(DateOnly Date, decimal Value);

/// <summary>The mean of a week's values (ISO week, Monday first) and how many there were.</summary>
public sealed record WeeklyAverage(DateOnly WeekStart, decimal Average, int Count);

/// <summary>
/// A tracked measure over time (§24, §25, §27): the first and latest values, the lowest and highest, the change since
/// the start (absolute and %), and weekly averages. Null values mean there are no entries.
/// </summary>
public sealed record SeriesSummary(
    SeriesPoint? Start, SeriesPoint? Current, SeriesPoint? Lowest, SeriesPoint? Highest,
    decimal? Change, decimal? ChangePercent, int Count, IReadOnlyList<WeeklyAverage> Weeks)
{
    public static readonly SeriesSummary Empty = new(null, null, null, null, null, null, 0, []);
}

/// <summary>A day of lifestyle tracking (§31–§33) against the targets in force.</summary>
public sealed record LifestyleDayFigures(DateOnly Date, decimal? SleepHours, int? SleepQuality, int? Steps, int? WaterMl);

/// <summary>
/// Lifestyle over a period: averages over the days logged, and goal completion = days that met the target ÷ days
/// logged (null without a target or entries).
/// </summary>
public sealed record LifestyleSummary(
    DateOnly Start, DateOnly End,
    int SleepNights, decimal? AverageSleepHours, decimal? AverageSleepQuality, int SleepGoalNights, decimal? SleepGoalPercent,
    int StepsDays, int? AverageSteps, int StepsGoalDays, decimal? StepsGoalPercent,
    int WaterDays, int? AverageWaterMl, int WaterGoalDays, decimal? WaterGoalPercent);

/// <summary>Progress statistics (§24–§27, §31–§33), pure.</summary>
public static class ProgressStatistics
{
    /// <summary>
    /// Summarizes a series. The start, current, lowest, highest and change cover every value (the client's whole
    /// history); weekly averages cover <paramref name="from"/>–<paramref name="to"/>. Equal lows or highs keep the
    /// earliest.
    /// </summary>
    public static SeriesSummary Summarize(IEnumerable<SeriesPoint> points, DateOnly from, DateOnly to, int decimals)
    {
        var ordered = points.OrderBy(x => x.Date).ToArray();
        if (ordered.Length == 0) return SeriesSummary.Empty;
        var start = ordered[0];
        var current = ordered[^1];
        var change = current.Value - start.Value;
        var weeks = ordered.Where(x => x.Date >= from && x.Date <= to)
            .GroupBy(x => NutritionAdherence.WeekStart(x.Date))
            .Select(week => new WeeklyAverage(week.Key, Round(week.Average(x => x.Value), decimals), week.Count()))
            .ToArray();
        return new SeriesSummary(start, current,
            ordered.OrderBy(x => x.Value).ThenBy(x => x.Date).First(),
            ordered.OrderByDescending(x => x.Value).ThenBy(x => x.Date).First(),
            Round(change, decimals), start.Value == 0 ? null : Round(change / start.Value * 100m, 1), ordered.Length, weeks);
    }

    /// <summary>How much of a target a value reaches, 0–100 (more counts as 100); null without a target or value.</summary>
    public static decimal? Completion(decimal? value, decimal? target) =>
        value is { } v && target is > 0 ? Round(Math.Min(100m, v / target.Value * 100m), 1) : null;

    public static LifestyleSummary Summarize(DateOnly start, DateOnly end, IEnumerable<LifestyleDayFigures> days, LifestyleTargetValues targets)
    {
        var range = days.Where(x => x.Date >= start && x.Date <= end).ToArray();
        var sleep = range.Where(x => x.SleepHours.HasValue).ToArray();
        var quality = sleep.Where(x => x.SleepQuality.HasValue).ToArray();
        var steps = range.Where(x => x.Steps.HasValue).ToArray();
        var water = range.Where(x => x.WaterMl > 0).ToArray();
        int sleepGoal = targets.SleepHours is { } sleepTarget ? sleep.Count(x => x.SleepHours >= sleepTarget) : 0;
        int stepsGoal = targets.Steps is { } stepsTarget ? steps.Count(x => x.Steps >= stepsTarget) : 0;
        int waterGoal = targets.WaterMl is { } waterTarget ? water.Count(x => x.WaterMl >= waterTarget) : 0;
        return new LifestyleSummary(start, end,
            sleep.Length, sleep.Length == 0 ? null : Round(sleep.Average(x => x.SleepHours!.Value), 2),
            quality.Length == 0 ? null : Round((decimal)quality.Average(x => x.SleepQuality!.Value), 1),
            sleepGoal, targets.SleepHours is null ? null : NutritionAdherence.Share(sleepGoal, sleep.Length),
            steps.Length, steps.Length == 0 ? null : (int)Round((decimal)steps.Average(x => x.Steps!.Value), 0),
            stepsGoal, targets.Steps is null ? null : NutritionAdherence.Share(stepsGoal, steps.Length),
            water.Length, water.Length == 0 ? null : (int)Round((decimal)water.Average(x => x.WaterMl!.Value), 0),
            waterGoal, targets.WaterMl is null ? null : NutritionAdherence.Share(waterGoal, water.Length));
    }

    private static decimal Round(decimal value, int decimals) => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
}
