using CoachHub.Application.Common.Models;
using CoachHub.Domain.DietPlanning;

namespace CoachHub.Application.FoodTracking;

/// <summary>Planned versus actual (§9, §10, §11): adherence, the diet in force on a day, and period figures.</summary>
public static class NutritionAdherence
{
    /// <summary>
    /// Daily adherence: for each of calories, protein, carbohydrates and fat with a target above zero,
    /// 100 − |actual − target| / target × 100 (never below 0), averaged. Null when there is no target.
    /// </summary>
    public static decimal? Percent(MacroAmounts target, MacroAmounts consumed)
    {
        decimal[] scores = new[]
        {
            Score(target.Calories, consumed.Calories), Score(target.Protein, consumed.Protein),
            Score(target.Carbohydrates, consumed.Carbohydrates), Score(target.Fat, consumed.Fat)
        }.OfType<decimal>().ToArray();
        return scores.Length == 0 ? null : Round(scores.Average());
    }

    /// <summary>The share of <paramref name="part"/> in <paramref name="whole"/> as a percentage; null for an empty whole.</summary>
    public static decimal? Share(int part, int whole) => whole <= 0 ? null : Round(part * 100m / whole);

    /// <summary>The diet version in force for the client on <paramref name="day"/> (see <see cref="PlanRevisions.InForce"/>).</summary>
    public static DietVersion? InForce(IEnumerable<DietVersion> versions, DateOnly day) =>
        PlanRevisions.InForce(versions, day, x => x.Plan.Id, x => x.Revision, x => x.Plan.CreatedAt);

    /// <summary>The figures of a period from its day rows; days after <paramref name="today"/> are not counted.</summary>
    public static NutritionPeriodSummary Summarize(DateOnly start, DateOnly end, IEnumerable<NutritionDayRow> rows, DateOnly today)
    {
        var elapsed = rows.Where(x => x.Date >= start && x.Date <= end && x.Date <= today).ToArray();
        var logged = elapsed.Where(x => x.EntryCount > 0).ToArray();
        var targets = logged.Where(x => x.Target is not null).Select(x => x.Target!).ToArray();
        var adherence = logged.Where(x => x.AdherencePercent.HasValue).Select(x => x.AdherencePercent!.Value).ToArray();
        return new NutritionPeriodSummary(
            start, end, elapsed.Length, logged.Length, elapsed.Count(x => x.IsCompleted),
            logged.Length == 0 ? null : logged.Aggregate(MacroAmounts.Zero, (sum, x) => sum + x.Consumed).DividedBy(logged.Length),
            targets.Length == 0 ? null : targets.Aggregate(MacroAmounts.Zero, (sum, x) => sum + x).DividedBy(targets.Length),
            adherence.Length == 0 ? null : Round(adherence.Average()),
            Share(logged.Length, elapsed.Length));
    }

    /// <summary>The Monday on or before <paramref name="day"/> (ISO 8601 weeks).</summary>
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Percentages show one decimal, rounding halves up (96.25 → 96.3).</summary>
    private static decimal Round(decimal percent) => decimal.Round(percent, 1, MidpointRounding.AwayFromZero);

    private static decimal? Score(decimal target, decimal actual) =>
        target <= 0 ? null : Math.Max(0m, 100m - Math.Abs(actual - target) / target * 100m);
}

/// <summary>A version of one of the client's diet plans.</summary>
public sealed record DietVersion(DietPlan Plan, DietPlanRevision Revision);
