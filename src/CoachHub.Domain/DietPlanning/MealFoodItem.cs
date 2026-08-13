using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class MealFoodItem : Entity
{
    private MealFoodItem() { }
    public Guid MealId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public int Order { get; private set; }
    public string? Notes { get; private set; }

    public static MealFoodItem Create(
        Guid id, Guid mealId, Guid foodItemId, decimal quantity, int order, string? notes) => new()
    {
        Id = DietPlanText.RequiredId(id, nameof(id)),
        MealId = DietPlanText.RequiredId(mealId, nameof(mealId)),
        FoodItemId = DietPlanText.RequiredId(foodItemId, nameof(foodItemId)),
        Quantity = DietPlanText.Quantity(quantity, nameof(quantity)),
        Order = DietPlanText.Order(order, nameof(order)),
        Notes = DietPlanText.Optional(notes, 1000, nameof(notes))
    };
}