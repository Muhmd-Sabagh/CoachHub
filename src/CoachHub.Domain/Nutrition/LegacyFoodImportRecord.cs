using CoachHub.Domain.Common;

namespace CoachHub.Domain.Nutrition;

public sealed class LegacyFoodImportRecord : Entity
{
    private LegacyFoodImportRecord() { }

    public int LegacyId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    public static LegacyFoodImportRecord Create(
        int legacyId,
        Guid foodItemId,
        DateTimeOffset importedAt)
    {
        if (legacyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(legacyId));
        }
        if (foodItemId == Guid.Empty)
        {
            throw new ArgumentException("A food item is required.", nameof(foodItemId));
        }

        return new LegacyFoodImportRecord
        {
            LegacyId = legacyId,
            FoodItemId = foodItemId,
            ImportedAt = importedAt
        };
    }
}