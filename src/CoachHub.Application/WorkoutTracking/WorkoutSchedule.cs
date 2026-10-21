using CoachHub.Application.Common.Models;
using CoachHub.Application.FoodTracking;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>A version of one of the client's workout plans, with how many training days it has.</summary>
public sealed record WorkoutVersion(WorkoutPlan Plan, WorkoutPlanRevision Revision, int DayCount);

/// <summary>Scheduled against completed workouts in a period (§20 frequency, §30 workout adherence).</summary>
public sealed record WorkoutAdherenceFigures(int ScheduledSessions, int CompletedSessions, decimal? AdherencePercent);

/// <summary>
/// The training schedule (owner decision, 2026-10-21): a plan's days are a <b>weekly rotation</b>. Each week the
/// client does every day of the version in force once, in order, on any days they like; the coach changes the
/// rotation by editing the plan (D4). Calendar placement of workout and rest days is P15 (§45).
/// </summary>
public static class WorkoutSchedule
{
    /// <summary>The workout version in force on a day: the same rule as diets (<see cref="PlanRevisions.InForce"/>).</summary>
    public static WorkoutVersion? InForce(IEnumerable<WorkoutVersion> versions, DateOnly day) =>
        PlanRevisions.InForce(versions, day, x => x.Plan.Id, x => x.Revision, x => x.Plan.CreatedAt);

    /// <summary>
    /// The next day of the rotation: the first day, in order, not yet done this week. Days done are matched by
    /// their place in the rotation, so a new version mid-week keeps the week's progress. Null when every day is done.
    /// </summary>
    public static WorkoutDay? NextDay(IEnumerable<WorkoutDay> days, IEnumerable<int> dayOrdersDoneThisWeek)
    {
        var done = dayOrdersDoneThisWeek.ToHashSet();
        return days.OrderBy(x => x.Order).FirstOrDefault(x => !done.Contains(x.Order));
    }

    /// <summary>
    /// Workout adherence over <paramref name="start"/>–<paramref name="end"/>, counting days up to
    /// <paramref name="today"/>. Each day with a plan in force expects a seventh of its rotation (a 4-day rotation
    /// expects 4 workouts a week), so a period expects the sum, rounded half up. Adherence = completed ÷ expected,
    /// at most 100%; null when nothing was expected. Weekly figures are periods of their own, so each week is capped
    /// on its own there.
    /// </summary>
    public static WorkoutAdherenceFigures Adherence(
        DateOnly start, DateOnly end, DateOnly today, Func<DateOnly, int> daysPerWeek, IEnumerable<DateOnly> completedDates)
    {
        var last = end < today ? end : today;
        var completed = completedDates.Count(x => x >= start && x <= last);
        var expectedDays = 0m;
        for (var day = start; day <= last; day = day.AddDays(1)) expectedDays += daysPerWeek(day);
        var scheduled = (int)decimal.Round(expectedDays / 7m, MidpointRounding.AwayFromZero);
        return new WorkoutAdherenceFigures(scheduled, completed, NutritionAdherence.Share(Math.Min(completed, scheduled), scheduled));
    }
}
