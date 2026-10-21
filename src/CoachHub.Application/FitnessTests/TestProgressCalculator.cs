namespace CoachHub.Application.FitnessTests;

/// <summary>A result as the progress calculation sees it; the recording time orders results of the same day.</summary>
public sealed record TestSample(DateOnly Date, decimal Value, DateTimeOffset RecordedAt);

/// <summary>Previous against current, best and progress of one test's results (§35, §36). Pure.</summary>
public static class TestProgressCalculator
{
    public static TestProgressResponse Compute(IEnumerable<TestSample> samples, bool higherIsBetter)
    {
        var ordered = samples.OrderBy(x => x.Date).ThenBy(x => x.RecordedAt).ToArray();
        if (ordered.Length == 0) return new TestProgressResponse(0, null, null, null, null, null, null, null, null);

        var first = ordered[0];
        var current = ordered[^1];
        var previous = ordered.Length > 1 ? ordered[^2] : null;
        // The best result; the earliest one when several are equal.
        var best = ordered.Aggregate((left, right) => higherIsBetter
            ? (right.Value > left.Value ? right : left)
            : (right.Value < left.Value ? right : left));

        decimal? fromPrevious = previous is null ? null : current.Value - previous.Value;
        decimal? fromFirst = ordered.Length > 1 ? current.Value - first.Value : null;
        decimal? percent = fromFirst is { } change && first.Value != 0
            ? Math.Round(change / first.Value * 100, 1, MidpointRounding.AwayFromZero)
            : null;
        TestTrend? trend = fromPrevious switch
        {
            null => null,
            0 => TestTrend.Unchanged,
            > 0 => higherIsBetter ? TestTrend.Improved : TestTrend.Declined,
            _ => higherIsBetter ? TestTrend.Declined : TestTrend.Improved
        };
        return new TestProgressResponse(ordered.Length, Point(first), previous is null ? null : Point(previous), Point(current),
            Point(best), fromPrevious, fromFirst, percent, trend);
    }

    private static TestPoint Point(TestSample sample) => new(sample.Date, sample.Value);
}
