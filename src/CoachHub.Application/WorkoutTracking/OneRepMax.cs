using CoachHub.Domain.Settings;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>
/// An estimated one-rep max formula (§22). The coach chooses one in their settings (Epley by default), so the
/// formula can be replaced without touching PRs or analytics.
/// </summary>
public interface IOneRepMaxFormula
{
    OneRepMaxFormula Kind { get; }

    /// <summary>
    /// The estimate in kg, rounded to two decimals; a single rep is the weight itself. Null without weight or reps,
    /// or above <see cref="OneRepMax.MaximumReps"/>, where rep-based estimates stop being meaningful.
    /// </summary>
    decimal? Estimate(decimal weightKg, int reps);
}

/// <summary>Epley: weight × (1 + reps / 30).</summary>
public sealed class EpleyFormula : IOneRepMaxFormula
{
    public OneRepMaxFormula Kind => OneRepMaxFormula.Epley;

    public decimal? Estimate(decimal weightKg, int reps) =>
        OneRepMax.Estimate(weightKg, reps, () => weightKg * (1m + reps / 30m));
}

/// <summary>Brzycki: weight × 36 / (37 − reps).</summary>
public sealed class BrzyckiFormula : IOneRepMaxFormula
{
    public OneRepMaxFormula Kind => OneRepMaxFormula.Brzycki;

    public decimal? Estimate(decimal weightKg, int reps) =>
        OneRepMax.Estimate(weightKg, reps, () => weightKg * 36m / (37m - reps));
}

public static class OneRepMax
{
    /// <summary>Sets of more reps than this get no estimate.</summary>
    public const int MaximumReps = 12;

    public static IOneRepMaxFormula For(OneRepMaxFormula kind) => kind switch
    {
        OneRepMaxFormula.Epley => new EpleyFormula(),
        OneRepMaxFormula.Brzycki => new BrzyckiFormula(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static decimal? Estimate(decimal weightKg, int reps, Func<decimal> formula) =>
        weightKg <= 0 || reps < 1 || reps > MaximumReps ? null
        : reps == 1 ? weightKg
        : decimal.Round(formula(), 2, MidpointRounding.AwayFromZero);
}
