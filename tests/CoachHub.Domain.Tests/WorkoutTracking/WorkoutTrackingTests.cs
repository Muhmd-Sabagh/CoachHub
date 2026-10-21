using CoachHub.Domain.Settings;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Domain.Tests.WorkoutTracking;

/// <summary>Workout logging (§18, D3), PR rows (§21) and the training settings (§22, §23).</summary>
public sealed class WorkoutTrackingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();

    [Fact]
    public void A_planned_workout_keeps_its_plan_references_and_exercises_in_order()
    {
        var planned = new PlannedWorkoutReference(Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid(), 1);
        var session = WorkoutSession.Start(Coach, Client, Today, " Push ", "دفع", planned, Now);
        var plannedExercise = Guid.NewGuid();
        var bench = session.AddExercise(Guid.NewGuid(), plannedExercise, WorkoutBlock.Main, "A1", Now);
        var extra = session.AddExercise(Guid.NewGuid(), null, WorkoutBlock.Main, null, Now);

        Assert.Equal(("Push", WorkoutSessionStatus.InProgress, 2, 1), (session.NameEn, session.Status, session.VersionNumber, session.DayOrder));
        Assert.True(session.IsPlanned);
        Assert.Equal([0, 1], session.Exercises.Select(x => x.Order));
        Assert.Equal(("A1", true, false), (bench.GroupLabel, bench.IsPlanned, extra.IsPlanned));
        Assert.Throws<ArgumentException>(() => session.AddExercise(Guid.NewGuid(), plannedExercise, WorkoutBlock.Main, null, Now));

        // Planned exercises are skipped, not removed; added ones can be removed.
        Assert.Throws<ArgumentException>(() => session.RemoveExercise(bench.Id, Now));
        session.RemoveExercise(extra.Id, Now);
        Assert.Single(session.Exercises);
    }

    [Fact]
    public void Sets_hold_typed_values_and_volume()
    {
        var exercise = Session().AddExercise(Guid.NewGuid(), null, WorkoutBlock.Main, null, Now);
        var set = exercise.LogSet(null, new LoggedSetValues(Reps: 8, WeightKg: 90m, Rir: 2, Rpe: 8.5m), Now);
        var timed = exercise.LogSet(null, new LoggedSetValues(DurationSeconds: 60), Now);
        var bodyweight = exercise.LogSet(null, new LoggedSetValues(Reps: 12), Now);

        Assert.Equal((0, 720m), (set.Order, set.VolumeKg));
        Assert.Equal((1, 0m), (timed.Order, timed.VolumeKg));
        Assert.Equal(0m, bodyweight.VolumeKg);

        set.Change(new LoggedSetValues(Reps: 10, WeightKg: 92.5m), Now);
        Assert.Equal(925m, set.VolumeKg);

        exercise.RemoveSet(timed.Id);
        Assert.Equal(2, exercise.Sets.Count);
    }

    [Theory]
    [InlineData(null, null, null, null, null)]            // nothing performed
    [InlineData(8, -1.0, null, null, null)]               // negative weight
    [InlineData(8, 1000.5, null, null, null)]             // too heavy
    [InlineData(8, 60.125, null, null, null)]             // three decimals
    [InlineData(8, null, 11, null, null)]                 // RIR above 10
    [InlineData(8, null, null, 8.3, null)]                // RPE not in halves
    [InlineData(8, null, null, 0.5, null)]                // RPE below 1
    [InlineData(1001, null, null, null, null)]            // too many reps
    [InlineData(null, null, null, null, 0)]               // zero duration
    public void Invalid_sets_are_rejected(int? reps, double? weight, int? rir, double? rpe, int? duration)
    {
        var exercise = Session().AddExercise(Guid.NewGuid(), null, WorkoutBlock.Main, null, Now);
        Assert.Throws<ArgumentException>(() => exercise.LogSet(null, new LoggedSetValues(
            Reps: reps, WeightKg: (decimal?)weight, Rir: rir, Rpe: (decimal?)rpe, DurationSeconds: duration), Now));
    }

    [Fact]
    public void Skipping_sets_and_exercises()
    {
        var exercise = Session().AddExercise(Guid.NewGuid(), Guid.NewGuid(), WorkoutBlock.Main, null, Now);
        var planned = Guid.NewGuid();
        var skipped = exercise.LogSet(planned, new LoggedSetValues(Status: LoggedSetStatus.Skipped), Now);
        Assert.False(skipped.IsCompleted);
        // A planned set is logged once; a skipped set has no values.
        Assert.Throws<ArgumentException>(() => exercise.LogSet(planned, new LoggedSetValues(Reps: 5), Now));
        Assert.Throws<ArgumentException>(() => exercise.LogSet(null, new LoggedSetValues(Status: LoggedSetStatus.Skipped, Reps: 5), Now));

        exercise.Skip(true);
        Assert.True(exercise.IsSkipped);
        Assert.Throws<ArgumentException>(() => exercise.LogSet(null, new LoggedSetValues(Reps: 5), Now));
        exercise.Skip(false);
        exercise.LogSet(null, new LoggedSetValues(Reps: 5), Now);
        // An exercise with completed sets cannot be skipped.
        Assert.Throws<ArgumentException>(() => exercise.Skip(true));
    }

    [Fact]
    public void A_workout_completes_once_and_only_with_a_completed_set()
    {
        var session = Session();
        var exercise = session.AddExercise(Guid.NewGuid(), null, WorkoutBlock.Main, null, Now);
        Assert.Throws<ArgumentException>(() => session.Complete(Now));
        exercise.LogSet(null, new LoggedSetValues(Status: LoggedSetStatus.Skipped), Now);
        Assert.Throws<ArgumentException>(() => session.Complete(Now));

        exercise.LogSet(null, new LoggedSetValues(Reps: 10, WeightKg: 50m), Now);
        session.Complete(Now.AddHours(1));
        Assert.Equal((WorkoutSessionStatus.Completed, Now.AddHours(1)), (session.Status, session.CompletedAt));
        Assert.Throws<InvalidOperationException>(() => session.Complete(Now));
    }

    [Fact]
    public void A_free_workout_needs_a_name_and_limits_its_size()
    {
        Assert.Throws<ArgumentException>(() => WorkoutSession.Start(Coach, Client, Today, " ", null, null, Now));
        Assert.Throws<ArgumentException>(() => WorkoutSession.Start(Guid.Empty, Client, Today, "Run", null, null, Now));
        var session = Session();
        Assert.False(session.IsPlanned);
        var exercise = session.AddExercise(Guid.NewGuid(), null, WorkoutBlock.Main, null, Now);
        for (var i = 0; i < LoggedExercise.MaximumSets; i++) exercise.LogSet(null, new LoggedSetValues(Reps: 1), Now);
        Assert.Throws<ArgumentException>(() => exercise.LogSet(null, new LoggedSetValues(Reps: 1), Now));
    }

    [Fact]
    public void Records_beat_the_previous_best()
    {
        var record = PersonalRecord.Create(Coach, Client, Guid.NewGuid(), PersonalRecordType.Weight, 90m, 85m, 90m, 8,
            Guid.NewGuid(), Guid.NewGuid(), Today);
        Assert.Equal((90m, 85m), (record.Value, record.PreviousValue));
        Assert.Throws<ArgumentException>(() => PersonalRecord.Create(Coach, Client, Guid.NewGuid(), PersonalRecordType.Weight, 90m, 90m,
            90m, 8, Guid.NewGuid(), null, Today));
        Assert.Throws<ArgumentException>(() => PersonalRecord.Create(Coach, Client, Guid.NewGuid(), PersonalRecordType.Volume, 0m, null,
            null, null, Guid.NewGuid(), null, Today));
        Assert.Throws<ArgumentException>(() => PersonalRecord.Create(Coach, Client, Guid.NewGuid(), (PersonalRecordType)9, 1m, null,
            null, null, Guid.NewGuid(), null, Today));
    }

    [Fact]
    public void Coach_settings_hold_the_training_defaults()
    {
        var settings = CoachSettings.CreateDefault(Coach, Now);
        Assert.Equal((OneRepMaxFormula.Epley, 0m), (settings.OneRepMaxFormula, settings.SecondaryMuscleWeight));

        settings.UpdateTraining(OneRepMaxFormula.Brzycki, 0.5m, Now);
        Assert.Equal((OneRepMaxFormula.Brzycki, 0.5m), (settings.OneRepMaxFormula, settings.SecondaryMuscleWeight));
        Assert.Throws<ArgumentException>(() => settings.UpdateTraining(OneRepMaxFormula.Epley, 1.5m, Now));
        Assert.Throws<ArgumentException>(() => settings.UpdateTraining(OneRepMaxFormula.Epley, -0.1m, Now));
        Assert.Throws<ArgumentException>(() => settings.UpdateTraining(OneRepMaxFormula.Epley, 0.333m, Now));
        Assert.Throws<ArgumentException>(() => settings.UpdateTraining((OneRepMaxFormula)7, 0m, Now));
    }

    private static WorkoutSession Session() => WorkoutSession.Start(Coach, Client, Today, "Free", null, null, Now);
}
