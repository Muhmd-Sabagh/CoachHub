using System.Globalization;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.WorkoutPlanning;

/// <summary>
/// An exercise's sets as short text for a table (PDF, lists): "1 warm-up + 3", "8–12" or "12/10/8", "60 kg",
/// "RIR 2", "3-1-1-0", "90 s". Empty when nothing of that kind is prescribed.
/// </summary>
public sealed record PrescriptionSummary(string Sets, string Reps, string Load, string Effort, string Tempo, string Rest)
{
    public static PrescriptionSummary Of(IReadOnlyCollection<PrescribedSetResponse> sets, bool arabic = false)
    {
        var ordered = sets.OrderBy(x => x.Order).ToArray();
        var working = ordered.Where(x => x.Type != SetType.WarmUp).ToArray();
        var warmUps = ordered.Length - working.Length;
        var drops = working.Count(x => x.Type == SetType.Drop);
        var main = working.Length - drops;
        var count = new List<string>();
        if (warmUps > 0) count.Add($"{warmUps} {(arabic ? "إحماء" : "warm-up")}");
        if (main > 0) count.Add(main.ToString(CultureInfo.InvariantCulture));
        if (drops > 0) count.Add($"{drops} {(arabic ? "دروب" : "drop")}");
        var shown = working.Length > 0 ? working : ordered;
        return new(
            string.Join(" + ", count),
            Join(shown.Select(RepsOf)),
            Join(shown.Select(x => x.LoadKg is { } load ? Number(load) : null), " kg"),
            Join(shown.Select(EffortOf)),
            Join(shown.Select(x => x.Tempo?.ToString())),
            Join(shown.Select(x => x.RestSeconds is { } rest ? Seconds(rest) : null)));
    }

    private static string? RepsOf(PrescribedSetResponse set) => set switch
    {
        { RepsMin: { } min, RepsMax: { } max } when max != min => $"{min}–{max}",
        { RepsMin: { } exact } => exact.ToString(CultureInfo.InvariantCulture),
        { DurationSeconds: { } duration } => Seconds(duration),
        _ => null
    };

    private static string? EffortOf(PrescribedSetResponse set)
    {
        var parts = new List<string>();
        if (set.Rir is { } rir) parts.Add($"RIR {rir}");
        if (set.Rpe is { } rpe) parts.Add($"RPE {Number(rpe)}");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>One value when every set shares it, otherwise each set's value: "12/10/8".</summary>
    private static string Join(IEnumerable<string?> values, string suffix = "")
    {
        var items = values.ToArray();
        if (items.All(x => x is null)) return string.Empty;
        var distinct = items.Distinct().ToArray();
        return (distinct.Length == 1 ? distinct[0]! : string.Join("/", items.Select(x => x ?? "–"))) + suffix;
    }

    private static string Seconds(int seconds) =>
        seconds >= 120 && seconds % 60 == 0 ? $"{seconds / 60} min" : $"{seconds} s";

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
