using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.Tests.WorkoutPlanning;

public sealed class WorkoutPlanTests
{
    [Fact]
    public void Workout_entities_keep_blocks_groups_and_optional_arabic()
    {
        var plan = WorkoutPlan.Create(Guid.NewGuid(), "  Strength ", null, null, DateTimeOffset.UtcNow);
        var revisionId = Guid.NewGuid();
        var day = WorkoutDay.Create(Guid.NewGuid(), revisionId, " Push ", " دفع ", "Upper", null, 0);
        var group = WorkoutSetGroup.Create(Guid.NewGuid(), revisionId, day.Id, SetGroupType.Superset, 90, null);
        var exercise = WorkoutExercise.Create(Guid.NewGuid(), revisionId, day.Id, Guid.NewGuid(), 0,
            " controlled ", WorkoutBlock.Main, group.Id);

        Assert.Equal("Strength", plan.NameEn);
        Assert.Equal("دفع", day.NameAr);
        Assert.Equal((WorkoutBlock.Main, (Guid?)group.Id, "controlled"), (exercise.Block, exercise.SetGroupId, exercise.Notes));
        Assert.Throws<ArgumentException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), revisionId, day.Id, Guid.NewGuid(), 0, null, (WorkoutBlock)7));
        Assert.Throws<ArgumentException>(() => WorkoutSetGroup.Create(Guid.NewGuid(), revisionId, day.Id, SetGroupType.GiantSet, 4000, null));
    }

    [Fact]
    public void Workout_exercise_rejects_empty_identifiers_and_negative_order()
    {
        Assert.Throws<ArgumentException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 0, null));
        Assert.Throws<ArgumentException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1, null));
    }
}
