using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.DietPlanning;

public sealed record DietPlanAggregate(
    DietPlan Plan,
    IReadOnlyList<DietPlanNote> Notes,
    IReadOnlyList<DietPlanVersion> Versions,
    IReadOnlyList<Meal> Meals,
    IReadOnlyList<MealFoodItem> FoodItems,
    IReadOnlyList<DietReplacementGroup> ReplacementGroups,
    IReadOnlyList<DietReplacementOption> ReplacementOptions);

public interface IDietPlanRepository
{
    Task<DietPlanAggregate?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, FoodItem>> FindFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>The client's current coach, or null when the client is not visible.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    Task AddAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken);
    Task ReplaceChildrenAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken);
    Task DeleteAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
