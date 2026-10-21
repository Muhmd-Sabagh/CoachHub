using CoachHub.Domain.Calendar;

namespace CoachHub.Application.Calendar;

/// <summary>A planned day of the training calendar: a rotation day, or rest.</summary>
public sealed record PlannedDay(DateOnly Date, Guid PlanId, RotationDay? Workout);

/// <summary>
/// Places the weekly rotation (P10) on the coach's training weekdays (§45, owner decision 2026-10-22). In each ISO
/// week, Monday first, the rotation's days fill the training weekdays in order: Day 1 on the first, Day 2 on the
/// second… Training weekdays beyond the rotation, and the other days, are rest days. A rotation longer than the
/// training weekdays leaves its last days unplaced that week. Days without a plan in force get nothing. Pure.
/// </summary>
public static class TrainingCalendar
{
    public static IReadOnlyList<PlannedDay> Plan(
        DateOnly from, DateOnly to, TrainingWeekdays weekdays, Func<DateOnly, RotationInForce?> inForce)
    {
        var planned = new List<PlannedDay>();
        if (weekdays == TrainingWeekdays.None) return planned;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (inForce(date) is not { } rotation) continue;
            if (!weekdays.HasFlag(TrainingWeekdaySet.Flag(date.DayOfWeek)))
            {
                planned.Add(new PlannedDay(date, rotation.PlanId, null));
                continue;
            }
            var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            var place = 0;
            for (var day = weekStart; day < date; day = day.AddDays(1))
                if (weekdays.HasFlag(TrainingWeekdaySet.Flag(day.DayOfWeek))) place++;
            var days = rotation.Days.OrderBy(x => x.Order).ToArray();
            planned.Add(new PlannedDay(date, rotation.PlanId, place < days.Length ? days[place] : null));
        }
        return planned;
    }
}
