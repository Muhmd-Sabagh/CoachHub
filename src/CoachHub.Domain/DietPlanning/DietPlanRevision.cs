using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>
/// A version of a diet plan (Diet V1 → V2 → V3, §59) with its program dates (§4, §12) and its
/// targets: calories, macros, and meal and snack counts (§4).
/// </summary>
public sealed class DietPlanRevision : PlanRevision
{
    private DietPlanRevision() { }

    public override PlanKind Kind => PlanKind.Diet;

    public decimal? TargetCalories { get; private set; }
    public decimal? TargetProtein { get; private set; }
    public decimal? TargetCarbohydrates { get; private set; }
    public decimal? TargetFat { get; private set; }
    public int? TargetMealCount { get; private set; }
    public int? TargetSnackCount { get; private set; }

    public DietTargets Targets => new(TargetCalories, TargetProtein, TargetCarbohydrates, TargetFat, TargetMealCount, TargetSnackCount);

    /// <summary>Sets the version's targets; archived versions are read-only.</summary>
    public void SetTargets(DietTargets targets)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(targets);
        targets.Validate();
        (TargetCalories, TargetProtein, TargetCarbohydrates, TargetFat, TargetMealCount, TargetSnackCount) =
            (targets.Calories, targets.Protein, targets.Carbohydrates, targets.Fat, targets.MealCount, targets.SnackCount);
    }

    public static DietPlanRevision Create(
        Guid planId, int versionNumber, PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate,
        Guid? createdByUserId, DateTimeOffset createdAt)
    {
        var revision = new DietPlanRevision();
        revision.Initialize(planId, versionNumber, status, startDate, endDate, createdByUserId, createdAt);
        return revision;
    }
}
