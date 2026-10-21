using CoachHub.Application.Calendar;
using CoachHub.Application.Dashboards;
using CoachHub.Domain.Calendar;

namespace CoachHub.Application.Tests.Calendar;

public sealed class CalendarAndRevenueTests
{
    private static readonly Guid Plan = Guid.NewGuid();
    // 2026-10-19 is a Monday.
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private static RotationInForce Rotation(params string[] days) =>
        new(Plan, "Rotation", days.Select((name, index) => new RotationDay(index, name)).ToArray());

    [Fact]
    public void The_rotation_fills_the_training_weekdays_in_order_and_the_rest_are_rest_days()
    {
        var weekdays = TrainingWeekdays.Monday | TrainingWeekdays.Wednesday | TrainingWeekdays.Friday;
        var plan = TrainingCalendar.Plan(Monday, Monday.AddDays(13), weekdays, _ => Rotation("Push", "Pull", "Legs"));
        Assert.Equal(14, plan.Count);
        Assert.Equal(["Push", null, "Pull", null, "Legs", null, null], plan.Take(7).Select(x => x.Workout?.Name));
        // Every week starts the rotation again.
        Assert.Equal("Push", plan[7].Workout!.Name);
    }

    [Fact]
    public void A_range_starting_mid_week_counts_the_week_from_monday()
    {
        var weekdays = TrainingWeekdays.Monday | TrainingWeekdays.Thursday;
        var plan = TrainingCalendar.Plan(Monday.AddDays(2), Monday.AddDays(3), weekdays, _ => Rotation("A", "B"));
        Assert.Equal([null, "B"], plan.Select(x => x.Workout?.Name));
    }

    [Fact]
    public void Extra_training_weekdays_rest_and_extra_rotation_days_are_left_out()
    {
        var all = TrainingCalendar.Plan(Monday, Monday.AddDays(6), TrainingWeekdays.All, _ => Rotation("A", "B"));
        Assert.Equal(["A", "B", null, null, null, null, null], all.Select(x => x.Workout?.Name));
        var one = TrainingCalendar.Plan(Monday, Monday.AddDays(6), TrainingWeekdays.Tuesday, _ => Rotation("A", "B", "C"));
        Assert.Equal(["A"], one.Where(x => x.Workout is not null).Select(x => x.Workout!.Name));
    }

    [Fact]
    public void Nothing_is_planned_without_weekdays_or_a_plan_in_force()
    {
        Assert.Empty(TrainingCalendar.Plan(Monday, Monday.AddDays(6), TrainingWeekdays.None, _ => Rotation("A")));
        var halfWeek = TrainingCalendar.Plan(Monday, Monday.AddDays(6), TrainingWeekdays.All, day => day < Monday.AddDays(3) ? null : Rotation("A"));
        Assert.Equal(4, halfWeek.Count);
        Assert.Equal(Monday.AddDays(3), halfWeek[0].Date);
    }

    [Fact]
    public void Training_days_need_at_least_one_known_weekday()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("weekdays", Assert.Throws<ArgumentException>(() => TrainingDays.Create(Guid.NewGuid(), Guid.NewGuid(), TrainingWeekdays.None, now)).ParamName);
        Assert.Equal("weekdays", Assert.Throws<ArgumentException>(() => TrainingDays.Create(Guid.NewGuid(), Guid.NewGuid(), (TrainingWeekdays)128, now)).ParamName);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], TrainingWeekdaySet.Days(TrainingWeekdaySet.Of([DayOfWeek.Sunday, DayOfWeek.Monday])));
    }

    [Fact]
    public void Events_follow_the_sharing_and_time_rules()
    {
        var now = DateTimeOffset.UtcNow;
        var coach = Guid.NewGuid();
        var client = Guid.NewGuid();
        var day = new DateOnly(2026, 11, 2);
        var shared = CalendarEvent.Create(coach, client, CalendarEventKind.Event, " Competition ", null, day, new TimeOnly(9, 0), new TimeOnly(11, 0), true, now);
        Assert.Equal(("Competition", true), (shared.Title, shared.VisibleToClient));
        Assert.Equal("visibleToClient", Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(coach, client, CalendarEventKind.FollowUp, "Call", null, day, null, null, true, now)).ParamName);
        Assert.Equal("visibleToClient", Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(coach, null, CalendarEventKind.Event, "Holiday", null, day, null, null, true, now)).ParamName);
        Assert.Equal("endTime", Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(coach, client, CalendarEventKind.Event, "Late", null, day, new TimeOnly(10, 0), new TimeOnly(9, 0), false, now)).ParamName);
        Assert.Equal("endTime", Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(coach, client, CalendarEventKind.Event, "No start", null, day, null, new TimeOnly(9, 0), false, now)).ParamName);
        Assert.Equal("title", Assert.Throws<ArgumentException>(() =>
            CalendarEvent.Create(coach, client, CalendarEventKind.Event, " ", null, day, null, null, false, now)).ParamName);
    }

    [Fact]
    public void Revenue_is_settled_payments_minus_refunds_per_currency()
    {
        var coachA = Guid.NewGuid();
        var coachB = Guid.NewGuid();
        var egp = Guid.NewGuid();
        var usd = Guid.NewGuid();
        RevenueRow Row(Guid coach, Guid currency, string code, decimal amount, bool refund) =>
            new(coach, coach == coachA ? "Alex" : "Bea", currency, code, null, "No package", null, "No payment account", amount, refund);
        RevenueRow[] rows =
        [
            Row(coachA, egp, "EGP", 1000, false),
            Row(coachA, egp, "EGP", 500, false),
            Row(coachA, egp, "EGP", 200, true),
            Row(coachB, egp, "EGP", 300, false),
            Row(coachB, usd, "USD", 50, false),
            // A refund of an earlier period's payment counts when it is recorded, so the net can be negative.
            Row(coachB, usd, "USD", 80, true),
        ];

        var byCurrency = RevenueCalculator.ByCurrency(rows);
        Assert.Equal([("EGP", 3, 1, 1800m, 200m, 1600m), ("USD", 1, 1, 50m, 80m, -30m)],
            byCurrency.Select(x => (x.CurrencyCode, x.Payments, x.Refunds, x.Settled, x.Refunded, x.Net)));
        var byCoach = RevenueCalculator.ByCoach(rows);
        Assert.Equal([("Alex", "EGP", 1300m), ("Bea", "EGP", 300m), ("Bea", "USD", -30m)], byCoach.Select(x => (x.Label, x.CurrencyCode, x.Net)));
        Assert.Empty(RevenueCalculator.ByCurrency([]));
    }
}
