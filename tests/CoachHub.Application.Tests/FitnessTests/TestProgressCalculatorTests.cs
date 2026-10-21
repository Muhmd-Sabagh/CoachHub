using CoachHub.Application.FitnessTests;

namespace CoachHub.Application.Tests.FitnessTests;

public sealed class TestProgressCalculatorTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static TestSample Sample(int day, decimal value, int minute = 0) =>
        new(new DateOnly(2026, 10, day), value, At.AddDays(day).AddMinutes(minute));

    [Fact]
    public void No_results_have_no_progress()
    {
        var progress = TestProgressCalculator.Compute([], true);
        Assert.Equal(0, progress.Count);
        Assert.Null(progress.Current);
        Assert.Null(progress.Trend);
    }

    [Fact]
    public void A_single_result_is_first_current_and_best_without_change()
    {
        var progress = TestProgressCalculator.Compute([Sample(3, 20)], true);
        Assert.Equal((1, 20m, 20m, 20m), (progress.Count, progress.First!.Value, progress.Current!.Value, progress.Best!.Value));
        Assert.Null(progress.Previous);
        Assert.Null(progress.ChangeFromPrevious);
        Assert.Null(progress.ChangeFromFirst);
        Assert.Null(progress.PercentChangeFromFirst);
        Assert.Null(progress.Trend);
    }

    [Fact]
    public void Higher_is_better_compares_current_with_previous_and_first()
    {
        // Out of order on purpose: results are ordered by date, then by when they were recorded.
        var progress = TestProgressCalculator.Compute([Sample(10, 18), Sample(1, 15), Sample(20, 24), Sample(10, 26, minute: -5)], true);
        Assert.Equal(4, progress.Count);
        Assert.Equal((new DateOnly(2026, 10, 1), 15m), (progress.First!.Date, progress.First.Value));
        Assert.Equal((new DateOnly(2026, 10, 10), 18m), (progress.Previous!.Date, progress.Previous.Value));
        Assert.Equal(24m, progress.Current!.Value);
        Assert.Equal(26m, progress.Best!.Value);
        Assert.Equal((6m, 9m, 60m, TestTrend.Improved), (progress.ChangeFromPrevious!.Value, progress.ChangeFromFirst!.Value,
            progress.PercentChangeFromFirst!.Value, progress.Trend!.Value));
    }

    [Fact]
    public void Lower_is_better_reads_a_faster_time_as_improved()
    {
        var progress = TestProgressCalculator.Compute([Sample(1, 720), Sample(8, 690), Sample(15, 700)], false);
        Assert.Equal(690m, progress.Best!.Value);
        Assert.Equal((10m, -20m, -2.8m, TestTrend.Declined), (progress.ChangeFromPrevious!.Value, progress.ChangeFromFirst!.Value,
            progress.PercentChangeFromFirst!.Value, progress.Trend!.Value));
        Assert.Equal(TestTrend.Improved, TestProgressCalculator.Compute([Sample(1, 720), Sample(8, 690)], false).Trend);
    }

    [Fact]
    public void Equal_results_are_unchanged_and_a_zero_start_has_no_percentage()
    {
        var progress = TestProgressCalculator.Compute([Sample(1, 0), Sample(2, 5), Sample(3, 5)], true);
        Assert.Equal(TestTrend.Unchanged, progress.Trend);
        Assert.Equal(5m, progress.ChangeFromFirst);
        Assert.Null(progress.PercentChangeFromFirst);
        // The earliest of equal best results.
        Assert.Equal(new DateOnly(2026, 10, 2), progress.Best!.Date);
    }
}
