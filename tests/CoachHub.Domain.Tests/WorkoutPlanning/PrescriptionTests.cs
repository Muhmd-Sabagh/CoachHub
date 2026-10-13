using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.Tests.WorkoutPlanning;

/// <summary>Typed prescriptions (§16, D3) and the exercise library's muscles and equipment (§17).</summary>
public sealed class PrescriptionTests
{
    private static PrescribedSet Set(int order, SetPrescription values) =>
        PrescribedSet.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), order, values);

    [Fact]
    public void A_set_keeps_every_typed_value()
    {
        var set = Set(1, new SetPrescription(SetType.Drop, 8, 12, 62.5m, 2, 8.5m, new Tempo(3, 1, 1, 0), 90, null, true, " slow "));

        Assert.Equal((SetType.Drop, 8, 12, 62.5m, 2, 8.5m), (set.Type, set.RepsMin!.Value, set.RepsMax!.Value, set.LoadKg!.Value, set.Rir!.Value, set.Rpe!.Value));
        Assert.Equal(("3-1-1-0", 90, true, "slow"), (set.Tempo!.ToString(), set.RestSeconds!.Value, set.IsSkippable, set.Notes));
        Assert.Null(Set(0, new SetPrescription(DurationSeconds: 45)).Tempo);
    }

    [Theory]
    [MemberData(nameof(InvalidSets))]
    public void Invalid_sets_are_rejected(int order, SetPrescription values) =>
        Assert.Throws<ArgumentException>(() => Set(order, values));

    public static TheoryData<int, SetPrescription> InvalidSets => new()
    {
        { 0, new SetPrescription(SetType.Drop) },                 // a drop set follows another set
        { 0, new SetPrescription(RepsMin: 12, RepsMax: 8) },
        { 0, new SetPrescription(RepsMax: 8) },                   // a maximum needs a minimum
        { 0, new SetPrescription(Rpe: 8.3m) },                    // RPE in steps of 0.5
        { 0, new SetPrescription(Rpe: 11) },
        { 0, new SetPrescription(Rir: 11) },
        { 0, new SetPrescription(LoadKg: 60.125m) },
        { 0, new SetPrescription(LoadKg: -1) },
        { 0, new SetPrescription(Tempo: new Tempo(3, 0, 1, 21)) },
        { 0, new SetPrescription(RestSeconds: 3601) },
        { 0, new SetPrescription(DurationSeconds: 0) },
        { 0, new SetPrescription((SetType)9) },
    };

    [Fact]
    public void Set_groups_take_two_or_three_to_ten_exercises()
    {
        Assert.True(WorkoutSetGroup.Fits(SetGroupType.Superset, 2));
        Assert.False(WorkoutSetGroup.Fits(SetGroupType.Superset, 3));
        Assert.False(WorkoutSetGroup.Fits(SetGroupType.GiantSet, 2));
        Assert.True(WorkoutSetGroup.Fits(SetGroupType.GiantSet, 3));
        Assert.False(WorkoutSetGroup.Fits(SetGroupType.GiantSet, 11));
    }

    [Fact]
    public void Exercises_keep_muscles_equipment_and_coaching_texts()
    {
        Guid chest = Guid.NewGuid(), triceps = Guid.NewGuid(), shoulders = Guid.NewGuid(), bench = Guid.NewGuid(), bar = Guid.NewGuid();
        var exercise = Exercise.Create(Guid.NewGuid(), new ExerciseValues("Bench press", null, Guid.NewGuid(),
            Difficulty: ExerciseDifficulty.Intermediate, PrimaryMuscleGroupId: chest, SecondaryMuscleGroupIds: [triceps, shoulders],
            EquipmentIds: [bench, bar], InstructionsEn: " Lower to the chest ", CoachingCuesAr: "شد الكتفين"));

        Assert.False(exercise.IsPlatform);
        Assert.Equal((ExerciseDifficulty.Intermediate, (Guid?)chest, "Lower to the chest", "شد الكتفين"),
            (exercise.Difficulty, exercise.PrimaryMuscleGroupId, exercise.InstructionsEn, exercise.CoachingCuesAr));
        Assert.Equal([triceps, shoulders], exercise.SecondaryMuscleGroupIds.OrderBy(x => x == shoulders));
        var kept = exercise.SecondaryMuscles.Single(x => x.MuscleGroupId == triceps);

        exercise.Update(new ExerciseValues("Bench press", null, exercise.ExerciseCategoryId, PrimaryMuscleGroupId: chest,
            SecondaryMuscleGroupIds: [triceps], EquipmentIds: [bench]));
        Assert.Same(kept, Assert.Single(exercise.SecondaryMuscles));
        Assert.Equal([bench], exercise.EquipmentIds);
        Assert.Null(exercise.Difficulty);

        Assert.Throws<ArgumentException>(() => exercise.Update(new ExerciseValues("Bench press", null, exercise.ExerciseCategoryId,
            PrimaryMuscleGroupId: chest, SecondaryMuscleGroupIds: [chest])));
        Assert.Throws<ArgumentException>(() => exercise.Update(new ExerciseValues("Bench press", null, exercise.ExerciseCategoryId,
            EquipmentIds: [bench, bench])));
        Assert.Throws<ArgumentException>(() => exercise.Update(new ExerciseValues("Bench press", null, exercise.ExerciseCategoryId,
            Difficulty: (ExerciseDifficulty)8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => exercise.Update(new ExerciseValues("Bench press", null, exercise.ExerciseCategoryId,
            InstructionsEn: new string('a', Exercise.MaximumTextLength + 1))));
    }
}
