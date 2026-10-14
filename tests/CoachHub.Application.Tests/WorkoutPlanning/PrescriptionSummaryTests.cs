using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.Tests.WorkoutPlanning;

public sealed class PrescriptionSummaryTests
{
    private static PrescribedSetResponse Set(
        int order, SetType type = SetType.Normal, int? min = null, int? max = null, decimal? load = null,
        int? rir = null, decimal? rpe = null, Tempo? tempo = null, int? rest = null, int? duration = null) =>
        new(Guid.NewGuid(), order, type, min, max, load, rir, rpe, tempo, rest, duration, false, null);

    [Fact]
    public void Identical_sets_collapse_to_one_value()
    {
        var summary = PrescriptionSummary.Of(
            [Set(0, min: 8, max: 12, load: 60, rir: 2, tempo: new Tempo(3, 1, 1, 0), rest: 90),
             Set(1, min: 8, max: 12, load: 60, rir: 2, tempo: new Tempo(3, 1, 1, 0), rest: 90),
             Set(2, min: 8, max: 12, load: 60, rir: 2, tempo: new Tempo(3, 1, 1, 0), rest: 90)]);

        Assert.Equal(new PrescriptionSummary("3", "8–12", "60 kg", "RIR 2", "3-1-1-0", "90 s"), summary);
    }

    [Fact]
    public void Warm_ups_and_drops_are_counted_apart_and_varying_sets_are_listed()
    {
        var summary = PrescriptionSummary.Of(
            [Set(0, SetType.WarmUp, min: 15, load: 20),
             Set(1, min: 12, load: 60, rpe: 7.5m, rest: 120),
             Set(2, min: 10, load: 65, rpe: 8, rest: 120),
             Set(3, SetType.Drop, min: 8, load: 50, rpe: 9, rest: 120)]);

        Assert.Equal(("1 warm-up + 2 + 1 drop", "12/10/8", "60/65/50 kg", "RPE 7.5/RPE 8/RPE 9", "2 min"),
            (summary.Sets, summary.Reps, summary.Load, summary.Effort, summary.Rest));
        Assert.Equal("1 إحماء + 2 + 1 دروب", PrescriptionSummary.Of(
            [Set(0, SetType.WarmUp), Set(1), Set(2), Set(3, SetType.Drop)], arabic: true).Sets);
    }

    [Fact]
    public void Timed_sets_show_their_duration_and_empty_values_stay_empty()
    {
        var summary = PrescriptionSummary.Of([Set(0, duration: 45), Set(1, duration: 45)]);

        Assert.Equal(("2", "45 s", "", "", "", ""), (summary.Sets, summary.Reps, summary.Load, summary.Effort, summary.Tempo, summary.Rest));
        Assert.Equal("12/–", PrescriptionSummary.Of([Set(0, min: 12), Set(1)]).Reps);
    }
}
