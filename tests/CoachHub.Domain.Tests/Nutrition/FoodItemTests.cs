using CoachHub.Domain.Nutrition;

namespace CoachHub.Domain.Tests.Nutrition;

public sealed class FoodItemTests
{
    [Fact]
    public void Create_normalizes_names_and_allows_optional_arabic_and_media()
    {
        var categoryId = Guid.NewGuid();

        var food = FoodItem.Create(
            "  Chicken Breast  ",
            null,
            categoryId,
            " gram ",
            165,
            31,
            0,
            3.6m,
            null);

        Assert.Equal("Chicken Breast", food.NameEn);
        Assert.Null(food.NameAr);
        Assert.Equal("gram", food.MeasurementUnit);
        Assert.Null(food.MediaId);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(15001, 0, 0, 0)]
    public void Create_rejects_macros_outside_supported_ranges(
        decimal calories,
        decimal protein,
        decimal carbohydrates,
        decimal fat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FoodItem.Create(
            "Invalid",
            null,
            Guid.NewGuid(),
            "gram",
            calories,
            protein,
            carbohydrates,
            fat,
            null));
    }
}