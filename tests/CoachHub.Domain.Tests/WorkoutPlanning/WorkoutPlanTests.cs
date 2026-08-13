using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.Tests.WorkoutPlanning;

public sealed class WorkoutPlanTests
{
    [Fact]
    public void Workout_entities_preserve_flexible_prescription_text_and_optional_arabic()
    {
        var plan = WorkoutPlan.Create("  Strength ", null, null, DateTimeOffset.UtcNow);
        var day = WorkoutDay.Create(Guid.NewGuid(), plan.Id, " Push ", " دفع ", "Upper", null, 0);
        var exercise = WorkoutExercise.Create(Guid.NewGuid(), day.Id, Guid.NewGuid(), 0,
            " 3-4 ", "8-12", "90-120s", "2-0-1-0", "RPE 8", " controlled ");

        Assert.Equal("Strength", plan.NameEn);
        Assert.Equal("دفع", day.NameAr);
        Assert.Equal("3-4", exercise.Sets);
        Assert.Equal("controlled", exercise.Notes);
    }

    [Fact]
    public void Workout_exercise_rejects_empty_identifiers_and_negative_order()
    {
        Assert.Throws<ArgumentException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 0, null, null, null, null, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkoutExercise.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1, null, null, null, null, null, null));
    }
}
