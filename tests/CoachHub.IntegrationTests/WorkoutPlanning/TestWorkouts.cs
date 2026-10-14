using CoachHub.Application.WorkoutPlanning;
using CoachHub.Infrastructure.ReferenceData;

namespace CoachHub.IntegrationTests.WorkoutPlanning;

/// <summary>Seeded muscle groups and typed set prescriptions for workout tests.</summary>
public static class TestWorkouts
{
    public static Guid Chest => MuscleGroupSeed.All[0].Id;
    public static Guid Triceps => MuscleGroupSeed.All[4].Id;
    public static Guid Quadriceps => MuscleGroupSeed.All[6].Id;
    public static Guid Glutes => MuscleGroupSeed.All[8].Id;

    /// <summary><paramref name="count"/> identical sets.</summary>
    public static PrescribedSetInput[] Sets(
        int count, int? repsMin = null, int? repsMax = null, int? restSeconds = null, decimal? loadKg = null, int? rir = null) =>
        Enumerable.Range(0, count).Select(order => new PrescribedSetInput(
            Guid.NewGuid(), order, RepsMin: repsMin, RepsMax: repsMax, LoadKg: loadKg, Rir: rir, RestSeconds: restSeconds)).ToArray();
}
