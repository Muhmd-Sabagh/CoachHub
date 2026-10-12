using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.DietPlanning;

/// <summary>A diet plan with the content of one of its versions.</summary>
public sealed record DietPlanAggregate(
    DietPlan Plan,
    DietPlanRevision Revision,
    int CurrentVersionNumber,
    IReadOnlyList<DietPlanNote> Notes,
    IReadOnlyList<DietPlanVariant> Variants,
    IReadOnlyList<Meal> Meals,
    IReadOnlyList<MealFoodItem> FoodItems,
    IReadOnlyList<DietReplacementGroup> ReplacementGroups,
    IReadOnlyList<DietReplacementOption> ReplacementOptions);

public interface IDietPlanRepository
{
    /// <summary>The plan with one version: the current one when <paramref name="versionNumber"/> is null.</summary>
    Task<DietPlanAggregate?> FindAsync(Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken);
    /// <summary>Every version of a visible plan, oldest first; null when the plan is not visible.</summary>
    Task<IReadOnlyList<DietPlanRevision>?> ListRevisionsAsync(Guid planId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> FindAuthorNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    /// <summary>Foods the caller can see (platform foods and the coach's own), with their units.</summary>
    Task<IReadOnlyDictionary<Guid, FoodItem>> FindFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>
    /// Foods referenced by a plan the caller can already see, whoever owns them (after a client transfer a
    /// plan can reference the previous coach's custom foods).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, FoodItem>> FindReferencedFoodsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>The client's current coach, or null when the client is not visible.</summary>
    Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken);
    /// <summary>A new plan with its first version.</summary>
    Task AddAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken);
    /// <summary>Save in place: replaces the content of the (tracked, current) version.</summary>
    Task ReplaceContentAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken);
    /// <summary>Save as new version: stores the archived previous version and the next one together.</summary>
    Task AddRevisionAsync(DietPlanAggregate next, CancellationToken cancellationToken);
    /// <summary>Deletes the plan with its whole history.</summary>
    Task DeleteAsync(Guid planId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
