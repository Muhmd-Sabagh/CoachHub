using System.Text.Json.Serialization;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Progress;
using CoachHub.Application.Settings;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Adherence;

/// <summary>The areas of the adherence system (§30).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AdherenceArea>))]
public enum AdherenceArea
{
    Nutrition,
    Workout,
    CheckIn,
    Steps,
    Water,
    Sleep
}

/// <summary>An area's score (0–100, null when it cannot be scored) and its weight in the overall score.</summary>
public sealed record AdherenceAreaScore(AdherenceArea Area, decimal? Percent, int Weight);

/// <summary>
/// Adherence for a range (§30): each area and the overall score, weighted by the coach's adherence weights (§49).
/// Areas without a score (no plan, target or schedule) are left out and the others' weights rescaled.
/// </summary>
public sealed record AdherenceResponse(DateOnly From, DateOnly To, IReadOnlyList<AdherenceAreaScore> Areas, decimal? OverallPercent);

/// <summary>Adherence arithmetic (§30), pure.</summary>
public static class AdherenceScore
{
    /// <summary>Σ weight × score ÷ Σ weight over the areas that have a score and a weight; null when none do.</summary>
    public static decimal? Overall(IEnumerable<AdherenceAreaScore> areas)
    {
        var scored = areas.Where(x => x.Percent.HasValue && x.Weight > 0).ToArray();
        var weights = scored.Sum(x => x.Weight);
        return weights == 0 ? null
            : decimal.Round(scored.Sum(x => x.Percent!.Value * x.Weight) / weights, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A daily target's adherence: days that met the target ÷ days in the period (a day without an entry is missed).
    /// Null without a target or days.
    /// </summary>
    public static decimal? Daily(IReadOnlyCollection<decimal?> values, decimal? target) =>
        target is not > 0 || values.Count == 0 ? null
            : NutritionAdherence.Share(values.Count(x => x >= target), values.Count);

    public static IReadOnlyList<AdherenceAreaScore> Areas(
        AdherenceWeights weights, decimal? nutrition, decimal? workout, decimal? checkIn, decimal? steps, decimal? water, decimal? sleep) =>
    [
        new(AdherenceArea.Nutrition, nutrition, weights.Nutrition),
        new(AdherenceArea.Workout, workout, weights.Workout),
        new(AdherenceArea.CheckIn, checkIn, weights.CheckIn),
        new(AdherenceArea.Steps, steps, weights.Steps),
        new(AdherenceArea.Water, water, weights.Water),
        new(AdherenceArea.Sleep, sleep, weights.Sleep),
    ];
}

/// <summary>
/// The adherence system (§30): nutrition (P8: planned versus actual on logged days), workout (P10: completed versus
/// scheduled), check-in (P12: check-ins done versus due), and steps, water and sleep (P11: days that met the target).
/// </summary>
public sealed class AdherenceService(
    FoodTrackingService nutrition,
    WorkoutTrackingService training,
    ProgressService progress,
    CheckInService checkIns,
    ICheckInRepository clients,
    SettingsService settings)
{
    public async Task<AdherenceResponse> ScoreAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var client = await clients.FindClientAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
        var food = await nutrition.AnalyticsAsync(clientId, from, to, cancellationToken);
        var workouts = await training.AnalyticsAsync(clientId, from, to, cancellationToken);
        var lifestyle = await progress.LifestyleAsync(clientId, from, to, cancellationToken);
        var checkIn = await checkIns.AdherenceAsync(clientId, from, to, cancellationToken);
        var weights = (await settings.CoachAsync(client.CoachId, cancellationToken)).Weights;
        var areas = AdherenceScore.Areas(weights,
            food.Summary.AdherencePercent,
            workouts.Summary.AdherencePercent,
            checkIn.Percent,
            AdherenceScore.Daily(lifestyle.Days.Select(x => (decimal?)x.Steps?.Steps).ToArray(), lifestyle.Targets.Steps),
            AdherenceScore.Daily(lifestyle.Days.Select(x => x.WaterMl == 0 ? null : (decimal?)x.WaterMl).ToArray(), lifestyle.Targets.WaterMl),
            AdherenceScore.Daily(lifestyle.Days.Select(x => x.Sleep?.Hours).ToArray(), lifestyle.Targets.SleepHours));
        return new AdherenceResponse(from, to, areas, AdherenceScore.Overall(areas));
    }
}
