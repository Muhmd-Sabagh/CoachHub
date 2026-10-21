using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Common;
using CoachHub.Domain.Settings;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.Tests.WorkoutTracking;

/// <summary>Volume (§20), e1RM (§22), PR detection (§21), the weekly rotation and workout adherence, muscle analytics (§23).</summary>
public sealed class TrainingMathTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOneRepMaxFormula Epley = new EpleyFormula();

    [Fact]
    public void Epley_and_Brzycki_estimate_a_one_rep_max()
    {
        Assert.Equal(120m, Epley.Estimate(90m, 10));            // 90 × (1 + 10/30)
        Assert.Equal(114m, Epley.Estimate(90m, 8));             // 90 × 1.2667 = 114.00
        Assert.Equal(100m, Epley.Estimate(100m, 1));            // one rep is the weight
        var brzycki = OneRepMax.For(OneRepMaxFormula.Brzycki);
        Assert.Equal(OneRepMaxFormula.Brzycki, brzycki.Kind);
        Assert.Equal(111.72m, brzycki.Estimate(90m, 8));        // 90 × 36 / 29
        Assert.Equal(OneRepMaxFormula.Epley, OneRepMax.For(OneRepMaxFormula.Epley).Kind);

        // No estimate without weight or reps, or beyond 12 reps.
        Assert.Null(Epley.Estimate(0m, 5));
        Assert.Null(Epley.Estimate(60m, 0));
        Assert.Null(Epley.Estimate(60m, OneRepMax.MaximumReps + 1));
        Assert.NotNull(Epley.Estimate(60m, OneRepMax.MaximumReps));
    }

    [Fact]
    public void Volume_counts_working_and_drop_sets_but_not_warm_ups()
    {
        var sets = Sets(
            new LoggedSetValues(SetType.WarmUp, Reps: 10, WeightKg: 40m),
            new LoggedSetValues(Reps: 8, WeightKg: 80m),
            new LoggedSetValues(Reps: 8, WeightKg: 80m),
            new LoggedSetValues(SetType.Drop, Reps: 10, WeightKg: 60m),
            new LoggedSetValues(Status: LoggedSetStatus.Skipped));

        // 2 × 8 × 80 + 10 × 60 (Weight × Reps × Sets for equal sets, §20).
        Assert.Equal(1880m, TrainingSets.Volume(sets));
        var figures = TrainingAnalytics.Figures(sets, Epley);
        Assert.Equal((3, 26, 1880m), (figures.Sets, figures.Reps, figures.VolumeKg));
        // The top set and records come from working sets only: not the warm-up, not the drop set.
        Assert.Equal((80m, 8, 8), (figures.TopWeightKg!.Value, figures.TopWeightReps!.Value, figures.MaxReps!.Value));
        Assert.Equal(101.33m, figures.BestEstimatedOneRepMaxKg);
    }

    [Fact]
    public void Records_are_set_by_the_first_workout_and_each_later_improvement()
    {
        var first = Session(new DateOnly(2026, 10, 1), new LoggedSetValues(Reps: 8, WeightKg: 80m), new LoggedSetValues(Reps: 6, WeightKg: 85m));
        var tie = Session(new DateOnly(2026, 10, 3), new LoggedSetValues(Reps: 5, WeightKg: 85m));
        var better = Session(new DateOnly(2026, 10, 8), new LoggedSetValues(Reps: 8, WeightKg: 90m),
            new LoggedSetValues(SetType.Drop, Reps: 15, WeightKg: 50m));
        var bodyweight = Session(new DateOnly(2026, 10, 9), new LoggedSetValues(Reps: 12));

        // Order of input does not matter; workouts replay by date.
        var records = PersonalRecordDetector.Detect([better, bodyweight, tie, first], Epley);

        var weight = records.Where(x => x.Type == PersonalRecordType.Weight).ToArray();
        Assert.Equal([(85m, (decimal?)null), (90m, 85m)], weight.Select(x => (x.Value, x.PreviousValue)));
        Assert.Equal((90m, 8, better.SessionId), (weight[1].WeightKg!.Value, weight[1].Reps!.Value, weight[1].SessionId));

        // Most reps: 8 at first, then 12 bodyweight; the drop set's 15 does not count.
        Assert.Equal([8m, 12m], records.Where(x => x.Type == PersonalRecordType.Reps).Select(x => x.Value));

        // Volume per workout: 1150, 425 (no record), 720 + 750 = 1470.
        Assert.Equal([1150m, 1470m], records.Where(x => x.Type == PersonalRecordType.Volume).Select(x => x.Value));
        Assert.All(records.Where(x => x.Type == PersonalRecordType.Volume), x => Assert.Null(x.SetId));

        // e1RM: 80×8 → 101.33, 85×6 → 102, then 90×8 → 114.
        Assert.Equal([102m, 114m], records.Where(x => x.Type == PersonalRecordType.EstimatedOneRepMax).Select(x => x.Value));
        Assert.Empty(PersonalRecordDetector.Detect([], Epley));
    }

    [Fact]
    public void The_rotation_suggests_the_first_day_not_done_this_week()
    {
        var revision = Guid.NewGuid();
        var days = new[]
        {
            WorkoutDay.Create(Guid.NewGuid(), revision, "Legs", null, null, null, 2),
            WorkoutDay.Create(Guid.NewGuid(), revision, "Push", null, null, null, 0),
            WorkoutDay.Create(Guid.NewGuid(), revision, "Pull", null, null, null, 1),
        };
        Assert.Equal("Push", WorkoutSchedule.NextDay(days, [])!.NameEn);
        Assert.Equal("Pull", WorkoutSchedule.NextDay(days, [0])!.NameEn);
        Assert.Equal("Pull", WorkoutSchedule.NextDay(days, [0, 2])!.NameEn);
        Assert.Null(WorkoutSchedule.NextDay(days, [0, 1, 2]));
    }

    [Fact]
    public void Adherence_expects_the_rotation_once_a_week()
    {
        // Monday 2026-10-05 to Sunday 2026-10-18: two full weeks of a 3-day rotation.
        var start = new DateOnly(2026, 10, 5);
        var end = new DateOnly(2026, 10, 18);
        var today = new DateOnly(2026, 10, 25);
        // Week 1: four workouts (one extra); week 2: one.
        DateOnly[] done = [new(2026, 10, 5), new(2026, 10, 6), new(2026, 10, 7), new(2026, 10, 8), new(2026, 10, 14)];

        var figures = WorkoutSchedule.Adherence(start, end, today, _ => 3, done);
        Assert.Equal((6, 5, 83.3m), (figures.ScheduledSessions, figures.CompletedSessions, figures.AdherencePercent!.Value));
        // Each week on its own: the extra workout of week 1 is capped at 100%; week 2 is one of three.
        Assert.Equal(100m, WorkoutSchedule.Adherence(start, start.AddDays(6), today, _ => 3, done).AdherencePercent);
        Assert.Equal(33.3m, WorkoutSchedule.Adherence(start.AddDays(7), end, today, _ => 3, done).AdherencePercent);

        // A range that cuts weeks (Tuesday to Monday) still expects one rotation per seven days.
        var shifted = WorkoutSchedule.Adherence(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 12), today, _ => 2, [new(2026, 10, 12)]);
        Assert.Equal((2, 1, 50m), (shifted.ScheduledSessions, shifted.CompletedSessions, shifted.AdherencePercent!.Value));

        // Days after today are not expected yet: up to Wednesday of week 1, 3 × 3/7 ≈ 1, and 3 done caps at 100%.
        var midweek = WorkoutSchedule.Adherence(start, end, new DateOnly(2026, 10, 7), _ => 3, done);
        Assert.Equal((1, 3, 100m), (midweek.ScheduledSessions, midweek.CompletedSessions, midweek.AdherencePercent!.Value));

        // Without a plan nothing is expected.
        Assert.Null(WorkoutSchedule.Adherence(start, end, today, _ => 0, done).AdherencePercent);

        // A plan starting mid-week (Thursday) expects 4/7 of the week: 4 days of a 3-day rotation ≈ 2.
        var partial = WorkoutSchedule.Adherence(start, new DateOnly(2026, 10, 11), today, day => day >= new DateOnly(2026, 10, 8) ? 3 : 0, []);
        Assert.Equal((2, 0m), (partial.ScheduledSessions, partial.AdherencePercent!.Value));
    }

    [Fact]
    public void The_version_in_force_follows_the_diet_rule()
    {
        var created = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = WorkoutPlan.Create(Guid.NewGuid(), "Strength", null, Guid.NewGuid(), created);
        var v1 = WorkoutPlanRevision.Create(plan.Id, 1, PlanRevisionStatus.Active, null, null, null, created);
        v1.Archive(new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero));
        var v2 = WorkoutPlanRevision.Create(plan.Id, 2, PlanRevisionStatus.Active, null, null, null, created);
        var draft = WorkoutPlan.Create(Guid.NewGuid(), "Draft", null, Guid.NewGuid(), created.AddDays(1));
        var draftVersion = WorkoutPlanRevision.Create(draft.Id, 1, PlanRevisionStatus.Draft, null, null, null, created);
        WorkoutVersion[] versions = [new(plan, v1, 3), new(plan, v2, 4), new(draft, draftVersion, 5)];

        Assert.Equal(3, WorkoutSchedule.InForce(versions, new DateOnly(2026, 10, 9))!.DayCount);
        Assert.Equal(4, WorkoutSchedule.InForce(versions, new DateOnly(2026, 10, 10))!.DayCount);
    }

    [Fact]
    public void Muscle_distribution_counts_the_primary_and_weighted_secondary_muscles()
    {
        Guid chest = Guid.NewGuid(), triceps = Guid.NewGuid(), shoulders = Guid.NewGuid();
        Guid bench = Guid.NewGuid(), pushdown = Guid.NewGuid(), legacy = Guid.NewGuid();
        var session = WorkoutSession.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 20), "Push", null, null, Now);
        Log(session, bench, new LoggedSetValues(Reps: 10, WeightKg: 60m), new LoggedSetValues(Reps: 10, WeightKg: 60m),
            new LoggedSetValues(SetType.WarmUp, Reps: 10, WeightKg: 20m));
        Log(session, pushdown, new LoggedSetValues(Reps: 12, WeightKg: 25m));
        Log(session, legacy, new LoggedSetValues(Reps: 10));
        var muscles = new Dictionary<Guid, ExerciseMuscles>
        {
            [bench] = new(chest, [triceps, shoulders]),
            [pushdown] = new(triceps, []),
        };
        var names = new Dictionary<Guid, MuscleName> { [chest] = new("Chest", "الصدر"), [triceps] = new("Triceps", null), [shoulders] = new("Shoulders", null) };
        ExerciseMuscles Of(Guid id) => muscles.GetValueOrDefault(id) ?? ExerciseMuscles.None;

        // Primary only (default): chest 2 sets, triceps 1, and the exercise without a muscle is unassigned.
        var primary = TrainingAnalytics.Distribution(session.Exercises, Of, names, 0m);
        Assert.Equal([("Chest", 2m, 1200m), ("Triceps", 1m, 300m), ("Unassigned", 1m, 0m)],
            primary.Select(x => (x.NameEn, x.Sets, x.VolumeKg)));
        Assert.Equal(50m, primary[0].SharePercent);
        Assert.Null(primary[2].MuscleGroupId);

        // Secondary muscles at half weight: triceps 1 + 2 × 0.5, shoulders 2 × 0.5.
        var weighted = TrainingAnalytics.Distribution(session.Exercises, Of, names, 0.5m);
        Assert.Equal([("Chest", 2m), ("Triceps", 2m), ("Shoulders", 1m), ("Unassigned", 1m)], weighted.Select(x => (x.NameEn, x.Sets)));
        Assert.Equal(900m, weighted.Single(x => x.NameEn == "Triceps").VolumeKg);
        Assert.Equal(33.3m, weighted[0].SharePercent);
    }

    [Fact]
    public void A_period_summary_counts_completed_workouts_only()
    {
        var done = WorkoutSession.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 20), "A", null, null, Now);
        Log(done, Guid.NewGuid(), new LoggedSetValues(Reps: 5, WeightKg: 100m), new LoggedSetValues(Reps: 5, WeightKg: 100m));
        done.Complete(Now);
        var open = WorkoutSession.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 21), "B", null, null, Now);
        Log(open, Guid.NewGuid(), new LoggedSetValues(Reps: 5, WeightKg: 100m));

        var summary = TrainingAnalytics.Summarize(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), [done, open],
            _ => ExerciseMuscles.None, new Dictionary<Guid, MuscleName>(), 0m, new WorkoutAdherenceFigures(3, 1, 33.3m));
        Assert.Equal((1, 1, 3, 33.3m), (summary.CompletedSessions, summary.TrainingDays, summary.ScheduledSessions, summary.AdherencePercent!.Value));
        Assert.Equal((2, 10, 1000m), (summary.Sets, summary.Reps, summary.VolumeKg));
    }

    private static LoggedSet[] Sets(params LoggedSetValues[] values)
    {
        var session = WorkoutSession.Start(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1), "W", null, null, Now);
        return Log(session, Guid.NewGuid(), values).Sets.ToArray();
    }

    private static RecordSession Session(DateOnly date, params LoggedSetValues[] values)
    {
        var sets = Sets(values);
        return new RecordSession(Guid.NewGuid(), date, Now, sets);
    }

    private static LoggedExercise Log(WorkoutSession session, Guid exerciseId, params LoggedSetValues[] values)
    {
        var exercise = session.AddExercise(exerciseId, null, WorkoutBlock.Main, null, Now);
        foreach (var value in values) exercise.LogSet(null, value, Now);
        return exercise;
    }
}
