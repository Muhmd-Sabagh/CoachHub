using CoachHub.Application.Adherence;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Tests.Adherence;

/// <summary>The adherence system (§30): daily targets and the weighted overall score.</summary>
public sealed class AdherenceScoreTests
{
    [Fact]
    public void Daily_adherence_counts_missing_days_as_missed()
    {
        decimal?[] steps = [12000, 8000, null, 10000, 9999, 15000, null];
        Assert.Equal(42.9m, AdherenceScore.Daily(steps, 10000));          // 3 of 7 days
        Assert.Null(AdherenceScore.Daily(steps, null));
        Assert.Null(AdherenceScore.Daily([], 10000));
    }

    [Fact]
    public void The_overall_score_weights_the_areas_that_have_a_score()
    {
        var weights = new AdherenceWeights(35, 35, 15, 5, 5, 5);
        var areas = AdherenceScore.Areas(weights, nutrition: 80m, workout: 50m, checkIn: 100m, steps: 40m, water: null, sleep: 60m);
        Assert.Equal(6, areas.Count);
        Assert.Equal((AdherenceArea.CheckIn, 15), (areas[2].Area, areas[2].Weight));

        // (80×35 + 50×35 + 100×15 + 40×5 + 60×5) / 95; water has no target and is left out.
        Assert.Equal(68.9m, AdherenceScore.Overall(areas));

        // Zero weights do not count; nothing scored means no overall score.
        Assert.Equal(80m, AdherenceScore.Overall(AdherenceScore.Areas(new AdherenceWeights(100, 0, 0, 0, 0, 0), 80m, 10m, null, null, null, null)));
        Assert.Null(AdherenceScore.Overall(AdherenceScore.Areas(weights, null, null, null, null, null, null)));
    }
}
