using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutTracking;

/// <summary>What a personal record measures (§21).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PersonalRecordType>))]
public enum PersonalRecordType
{
    /// <summary>The heaviest weight lifted in a working set, in kg.</summary>
    Weight,

    /// <summary>The most reps in a working set.</summary>
    Reps,

    /// <summary>The most volume (weight × reps, kg) of the exercise in one workout.</summary>
    Volume,

    /// <summary>The highest estimated one-rep max of a working set, in kg (§22).</summary>
    EstimatedOneRepMax
}

/// <summary>
/// A personal record (§21): the client beat their previous best of an exercise. Every improvement is a row, so the
/// rows of an exercise and type are its PR history. Records are derived from completed workouts and rebuilt when one
/// changes; <see cref="WorkoutSessionId"/> and <see cref="LoggedSetId"/> are references only.
/// </summary>
public sealed class PersonalRecord : Entity, ITenantOwned
{
    private PersonalRecord() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public PersonalRecordType Type { get; private set; }

    /// <summary>The record: kg for weight, volume and e1RM; a count for reps.</summary>
    public decimal Value { get; private set; }

    /// <summary>The best before this record; null for the first.</summary>
    public decimal? PreviousValue { get; private set; }

    /// <summary>The set's weight and reps ("90 kg × 8"); for a volume record, the workout's sets are summed instead.</summary>
    public decimal? WeightKg { get; private set; }
    public int? Reps { get; private set; }
    public Guid WorkoutSessionId { get; private set; }
    public Guid? LoggedSetId { get; private set; }
    public DateOnly AchievedOn { get; private set; }

    public static PersonalRecord Create(
        Guid coachId, Guid clientId, Guid exerciseId, PersonalRecordType type, decimal value, decimal? previousValue,
        decimal? weightKg, int? reps, Guid workoutSessionId, Guid? loggedSetId, DateOnly achievedOn)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Unknown record type.", nameof(type));
        if (value <= 0) throw new ArgumentException("A record is above zero.", nameof(value));
        if (previousValue is { } previous && previous >= value)
            throw new ArgumentException("A record beats the previous best.", nameof(previousValue));
        return new PersonalRecord
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = TrackingText.Id(clientId, nameof(clientId)),
            ExerciseId = TrackingText.Id(exerciseId, nameof(exerciseId)),
            Type = type,
            Value = value,
            PreviousValue = previousValue,
            WeightKg = weightKg,
            Reps = reps,
            WorkoutSessionId = TrackingText.Id(workoutSessionId, nameof(workoutSessionId)),
            LoggedSetId = loggedSetId == Guid.Empty ? throw new ArgumentException("Set identifier cannot be empty.", nameof(loggedSetId)) : loggedSetId,
            AchievedOn = achievedOn
        };
    }

    /// <summary>Whether two records describe the same achievement (used to rebuild without churn).</summary>
    public bool SameAs(PersonalRecord other) =>
        ExerciseId == other.ExerciseId && Type == other.Type && Value == other.Value && PreviousValue == other.PreviousValue &&
        WeightKg == other.WeightKg && Reps == other.Reps && WorkoutSessionId == other.WorkoutSessionId &&
        LoggedSetId == other.LoggedSetId && AchievedOn == other.AchievedOn;
}
