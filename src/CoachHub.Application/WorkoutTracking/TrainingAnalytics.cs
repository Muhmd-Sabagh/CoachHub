using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>The muscles an exercise trains (§23), as the analytics count them.</summary>
public sealed record ExerciseMuscles(Guid? PrimaryMuscleGroupId, IReadOnlyList<Guid> SecondaryMuscleGroupIds)
{
    public static readonly ExerciseMuscles None = new(null, []);
}

public sealed record MuscleName(string NameEn, string? NameAr);

/// <summary>An exercise's figures in one workout (§19): work sets, reps, volume, the top set, most reps and best e1RM.</summary>
public sealed record ExerciseFigures(
    int Sets, int Reps, decimal VolumeKg, decimal? TopWeightKg, int? TopWeightReps, int? MaxReps, decimal? BestEstimatedOneRepMaxKg);

/// <summary>Training analytics (§20, §23), pure: per-exercise figures, period totals and the muscle distribution.</summary>
public static class TrainingAnalytics
{
    public static readonly MuscleName Unassigned = new("Unassigned", "غير محدد");

    public static ExerciseFigures Figures(IEnumerable<LoggedSet> sets, IOneRepMaxFormula formula)
    {
        var all = sets.ToArray();
        var work = all.Where(TrainingSets.IsWork).ToArray();
        var records = all.Where(TrainingSets.CountsForSetRecords).ToArray();
        var top = records.Where(x => x.WeightKg > 0 && x.Reps >= 1)
            .OrderByDescending(x => x.WeightKg).ThenByDescending(x => x.Reps).FirstOrDefault();
        var estimates = records.Select(x => x.WeightKg is { } weight && x.Reps is { } reps ? formula.Estimate(weight, reps) : null)
            .OfType<decimal>().ToArray();
        var reps = records.Where(x => x.Reps.HasValue).Select(x => x.Reps!.Value).ToArray();
        return new ExerciseFigures(
            work.Length, work.Sum(x => x.Reps ?? 0), TrainingSets.Volume(all),
            top?.WeightKg, top?.Reps, reps.Length == 0 ? null : reps.Max(), estimates.Length == 0 ? null : estimates.Max());
    }

    /// <summary>
    /// A period's totals from its completed workouts. Each work set counts one set and its volume for the exercise's
    /// primary muscle, and <paramref name="secondaryWeight"/> of both for each secondary muscle (0 by default:
    /// primary only). Muscles are ordered by sets, then volume.
    /// </summary>
    public static TrainingPeriodSummary Summarize(
        DateOnly start, DateOnly end, IEnumerable<WorkoutSession> sessions, Func<Guid, ExerciseMuscles> muscles,
        IReadOnlyDictionary<Guid, MuscleName> names, decimal secondaryWeight, WorkoutAdherenceFigures adherence)
    {
        var completed = sessions.Where(x => x.IsCompleted && x.Date >= start && x.Date <= end).ToArray();
        var exercises = completed.SelectMany(x => x.Exercises).ToArray();
        var work = exercises.SelectMany(x => x.Sets).Where(TrainingSets.IsWork).ToArray();
        return new TrainingPeriodSummary(
            start, end, completed.Length, completed.Select(x => x.Date).Distinct().Count(),
            adherence.ScheduledSessions, adherence.AdherencePercent,
            work.Length, work.Sum(x => x.Reps ?? 0), work.Sum(x => x.VolumeKg),
            Distribution(exercises, muscles, names, secondaryWeight));
    }

    public static IReadOnlyList<MuscleVolumeResponse> Distribution(
        IEnumerable<LoggedExercise> exercises, Func<Guid, ExerciseMuscles> muscles,
        IReadOnlyDictionary<Guid, MuscleName> names, decimal secondaryWeight)
    {
        var totals = new Dictionary<Guid, (decimal Sets, decimal Volume)>();
        var unassigned = (Sets: 0m, Volume: 0m);
        foreach (var exercise in exercises)
        {
            var work = exercise.Sets.Where(TrainingSets.IsWork).ToArray();
            if (work.Length == 0) continue;
            decimal sets = work.Length, volume = work.Sum(x => x.VolumeKg);
            var trained = muscles(exercise.ExerciseId);
            if (trained.PrimaryMuscleGroupId is not { } primary)
            {
                unassigned = (unassigned.Sets + sets, unassigned.Volume + volume);
                continue;
            }
            Add(primary, sets, volume);
            if (secondaryWeight <= 0) continue;
            foreach (var secondary in trained.SecondaryMuscleGroupIds.Where(x => x != primary).Distinct())
                Add(secondary, sets * secondaryWeight, volume * secondaryWeight);
        }

        var rows = totals.Select(x => (Id: (Guid?)x.Key, Name: names.GetValueOrDefault(x.Key) ?? Unassigned, x.Value.Sets, x.Value.Volume))
            .ToList();
        if (unassigned.Sets > 0) rows.Add((null, Unassigned, unassigned.Sets, unassigned.Volume));
        var all = rows.Sum(x => x.Sets);
        return rows.OrderByDescending(x => x.Sets).ThenByDescending(x => x.Volume).ThenBy(x => x.Name.NameEn)
            .Select(x => new MuscleVolumeResponse(x.Id, x.Name.NameEn, x.Name.NameAr, Round(x.Sets, 2), Round(x.Volume, 2),
                all == 0 ? 0 : Round(x.Sets * 100m / all, 1)))
            .ToArray();

        void Add(Guid muscle, decimal sets, decimal volume)
        {
            var current = totals.GetValueOrDefault(muscle);
            totals[muscle] = (current.Sets + sets, current.Volume + volume);
        }
    }

    private static decimal Round(decimal value, int decimals) => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);
}
