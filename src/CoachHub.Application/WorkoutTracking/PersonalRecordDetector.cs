using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>Which logged sets count for what (§19–§21).</summary>
public static class TrainingSets
{
    /// <summary>Work: completed working and drop sets. Warm-ups count for nothing; they are not training volume.</summary>
    public static bool IsWork(LoggedSet set) => set.IsCompleted && set.Type != SetType.WarmUp;

    /// <summary>Set records (weight, reps, e1RM) come from completed working sets; drop sets are not maximal efforts.</summary>
    public static bool CountsForSetRecords(LoggedSet set) => set.IsCompleted && set.Type == SetType.Normal;

    /// <summary>Volume of work sets: Σ weight × reps (§20 "Weight × Reps × Sets" when the sets are alike).</summary>
    public static decimal Volume(IEnumerable<LoggedSet> sets) => sets.Where(IsWork).Sum(x => x.VolumeKg);
}

/// <summary>An exercise's sets in one completed workout, for PR detection.</summary>
public sealed record RecordSession(Guid SessionId, DateOnly Date, DateTimeOffset CompletedAt, IReadOnlyList<LoggedSet> Sets);

/// <summary>A record found by <see cref="PersonalRecordDetector"/>.</summary>
public sealed record DetectedRecord(
    PersonalRecordType Type, decimal Value, decimal? PreviousValue, decimal? WeightKg, int? Reps,
    Guid SessionId, Guid? SetId, DateOnly Date);

/// <summary>
/// Personal records of one exercise (§21), pure. Workouts are replayed in order (date, then completion time); a
/// record is set whenever a workout beats the best so far, so the first workout sets the first records and every
/// later improvement adds one. Ties are not records.
/// </summary>
public static class PersonalRecordDetector
{
    public static IReadOnlyList<DetectedRecord> Detect(IEnumerable<RecordSession> sessions, IOneRepMaxFormula formula)
    {
        var records = new List<DetectedRecord>();
        var best = new Dictionary<PersonalRecordType, decimal>();

        void Consider(PersonalRecordType type, decimal value, decimal? weight, int? reps, RecordSession session, Guid? setId)
        {
            if (value <= 0) return;
            decimal? previous = best.TryGetValue(type, out var current) ? current : null;
            if (previous >= value) return;
            best[type] = value;
            records.Add(new DetectedRecord(type, value, previous, weight, reps, session.SessionId, setId, session.Date));
        }

        foreach (var session in sessions.OrderBy(x => x.Date).ThenBy(x => x.CompletedAt))
        {
            var sets = session.Sets.Where(TrainingSets.CountsForSetRecords).OrderBy(x => x.Order).ToArray();

            // Heaviest weight; between equal weights, the one with more reps.
            if (sets.Where(x => x.WeightKg > 0 && x.Reps >= 1).OrderByDescending(x => x.WeightKg).ThenByDescending(x => x.Reps)
                .FirstOrDefault() is { } heaviest)
                Consider(PersonalRecordType.Weight, heaviest.WeightKg!.Value, heaviest.WeightKg, heaviest.Reps, session, heaviest.Id);

            // Most reps; between equal reps, the heavier set.
            if (sets.Where(x => x.Reps >= 1).OrderByDescending(x => x.Reps).ThenByDescending(x => x.WeightKg ?? 0)
                .FirstOrDefault() is { } most)
                Consider(PersonalRecordType.Reps, most.Reps!.Value, most.WeightKg, most.Reps, session, most.Id);

            Consider(PersonalRecordType.Volume, TrainingSets.Volume(session.Sets), null, null, session, null);

            if (sets.Select(x => (Set: x, Estimate: x.WeightKg is { } weight && x.Reps is { } reps ? formula.Estimate(weight, reps) : null))
                .Where(x => x.Estimate.HasValue).OrderByDescending(x => x.Estimate).FirstOrDefault() is { Set: not null } strongest)
                Consider(PersonalRecordType.EstimatedOneRepMax, strongest.Estimate!.Value, strongest.Set.WeightKg, strongest.Set.Reps,
                    session, strongest.Set.Id);
        }
        return records;
    }
}
