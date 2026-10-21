using CoachHub.Application.Progress;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Tests.Progress;

/// <summary>Start / current / lowest / highest / change / weekly averages (§24, §25, §27) and lifestyle figures (§31–§33).</summary>
public sealed class ProgressStatisticsTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Fact]
    public void A_series_reports_start_current_extremes_change_and_weekly_averages()
    {
        SeriesPoint[] weights =
        [
            new(Monday.AddDays(9), 81.0m), new(Monday, 84.0m), new(Monday.AddDays(2), 83.0m),
            new(Monday.AddDays(7), 81.0m), new(Monday.AddDays(4), 85.0m),
        ];
        var summary = ProgressStatistics.Summarize(weights, Monday, Monday.AddDays(13), 2);

        Assert.Equal((Monday, 84.0m), (summary.Start!.Date, summary.Start.Value));
        Assert.Equal((Monday.AddDays(9), 81.0m), (summary.Current!.Date, summary.Current.Value));
        // Equal lows keep the earliest.
        Assert.Equal((Monday.AddDays(7), 81.0m), (summary.Lowest!.Date, summary.Lowest.Value));
        Assert.Equal(85.0m, summary.Highest!.Value);
        Assert.Equal((-3.0m, -3.6m, 5), (summary.Change!.Value, summary.ChangePercent!.Value, summary.Count));
        Assert.Equal([(Monday, 84.0m, 3), (Monday.AddDays(7), 81.0m, 2)], summary.Weeks.Select(x => (x.WeekStart, x.Average, x.Count)));

        // Weekly averages cover the range only; the history figures do not change.
        var lastWeek = ProgressStatistics.Summarize(weights, Monday.AddDays(7), Monday.AddDays(13), 2);
        Assert.Equal((84.0m, 1), (lastWeek.Start!.Value, lastWeek.Weeks.Count));
        Assert.Same(SeriesSummary.Empty, ProgressStatistics.Summarize([], Monday, Monday, 2));
    }

    [Fact]
    public void Completion_is_capped_at_the_target()
    {
        Assert.Equal(75m, ProgressStatistics.Completion(7500, 10000));
        Assert.Equal(100m, ProgressStatistics.Completion(12000, 10000));
        Assert.Null(ProgressStatistics.Completion(5000, null));
        Assert.Null(ProgressStatistics.Completion(null, 10000));
    }

    [Fact]
    public void Lifestyle_averages_logged_days_and_counts_goals_met()
    {
        LifestyleDayFigures[] days =
        [
            new(Monday, 7.5m, 4, 12000, 2500),
            new(Monday.AddDays(1), 6.0m, null, 6000, 1500),
            new(Monday.AddDays(2), null, null, null, null),
            new(Monday.AddDays(3), 8.25m, 2, 10000, 3000),
        ];
        var summary = ProgressStatistics.Summarize(Monday, Monday.AddDays(6), days, new LifestyleTargetValues(10000, 2500, 7.5m));

        Assert.Equal((3, 7.25m, 3.0m, 2, 66.7m), (summary.SleepNights, summary.AverageSleepHours!.Value, summary.AverageSleepQuality!.Value,
            summary.SleepGoalNights, summary.SleepGoalPercent!.Value));
        Assert.Equal((3, 9333, 2, 66.7m), (summary.StepsDays, summary.AverageSteps!.Value, summary.StepsGoalDays, summary.StepsGoalPercent!.Value));
        Assert.Equal((3, 2333, 2, 66.7m), (summary.WaterDays, summary.AverageWaterMl!.Value, summary.WaterGoalDays, summary.WaterGoalPercent!.Value));

        // Without targets there is no goal completion; without entries no averages.
        var none = ProgressStatistics.Summarize(Monday, Monday.AddDays(6), days, LifestyleTargetValues.None);
        Assert.Equal(((decimal?)null, (decimal?)null, (decimal?)null), (none.SleepGoalPercent, none.StepsGoalPercent, none.WaterGoalPercent));
        var empty = ProgressStatistics.Summarize(Monday.AddDays(10), Monday.AddDays(12), days, LifestyleTargetValues.None);
        Assert.Equal(((decimal?)null, (int?)null, (int?)null), (empty.AverageSleepHours, empty.AverageSteps, empty.AverageWaterMl));
    }
}
