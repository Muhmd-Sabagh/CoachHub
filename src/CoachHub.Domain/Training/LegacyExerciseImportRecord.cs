using CoachHub.Domain.Common;

namespace CoachHub.Domain.Training;

public sealed class LegacyExerciseImportRecord : Entity
{
    private LegacyExerciseImportRecord() { }

    public int LegacyId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    public static LegacyExerciseImportRecord Create(
        int legacyId,
        Guid exerciseId,
        DateTimeOffset importedAt)
    {
        if (legacyId <= 0) throw new ArgumentOutOfRangeException(nameof(legacyId));
        if (exerciseId == Guid.Empty) throw new ArgumentException("An exercise is required.", nameof(exerciseId));
        return new LegacyExerciseImportRecord
        {
            LegacyId = legacyId,
            ExerciseId = exerciseId,
            ImportedAt = importedAt
        };
    }
}