using CoachHub.Domain.Common;

namespace CoachHub.Domain.Nutrition;

public sealed class FoodItem : Entity
{
    public const decimal MaximumCaloriesPer100 = 15000m;
    public const decimal MaximumProteinPer100 = 5000m;
    public const decimal MaximumCarbohydratesPer100 = 5000m;
    public const decimal MaximumFatPer100 = 1000m;

    private FoodItem() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid FoodCategoryId { get; private set; }
    public string MeasurementUnit { get; private set; } = string.Empty;
    public decimal CaloriesPer100 { get; private set; }
    public decimal ProteinPer100 { get; private set; }
    public decimal CarbohydratesPer100 { get; private set; }
    public decimal FatPer100 { get; private set; }
    public Guid? MediaId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static FoodItem Create(
        string nameEn,
        string? nameAr,
        Guid foodCategoryId,
        string measurementUnit,
        decimal caloriesPer100,
        decimal proteinPer100,
        decimal carbohydratesPer100,
        decimal fatPer100,
        Guid? mediaId,
        bool isActive = true)
    {
        var food = new FoodItem();
        food.Update(
            nameEn,
            nameAr,
            foodCategoryId,
            measurementUnit,
            caloriesPer100,
            proteinPer100,
            carbohydratesPer100,
            fatPer100,
            mediaId,
            isActive);
        return food;
    }

    public void Update(
        string nameEn,
        string? nameAr,
        Guid foodCategoryId,
        string measurementUnit,
        decimal caloriesPer100,
        decimal proteinPer100,
        decimal carbohydratesPer100,
        decimal fatPer100,
        Guid? mediaId,
        bool isActive)
    {
        NameEn = Required(nameEn, 255, nameof(nameEn));
        NameAr = Optional(nameAr, 255, nameof(nameAr));
        if (foodCategoryId == Guid.Empty)
        {
            throw new ArgumentException("A food category is required.", nameof(foodCategoryId));
        }
        FoodCategoryId = foodCategoryId;
        MeasurementUnit = Required(measurementUnit, 50, nameof(measurementUnit));
        CaloriesPer100 = Macro(caloriesPer100, MaximumCaloriesPer100, nameof(caloriesPer100));
        ProteinPer100 = Macro(proteinPer100, MaximumProteinPer100, nameof(proteinPer100));
        CarbohydratesPer100 = Macro(
            carbohydratesPer100,
            MaximumCarbohydratesPer100,
            nameof(carbohydratesPer100));
        FatPer100 = Macro(fatPer100, MaximumFatPer100, nameof(fatPer100));
        MediaId = mediaId;
        IsActive = isActive;
    }

    private static decimal Macro(decimal value, decimal maximum, string parameterName)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between 0 and {maximum}.");
        }
        return value;
    }

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
        return normalized;
    }
}