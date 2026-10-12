using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>A version of a diet plan (Diet V1 → V2 → V3, §59) with its program dates (§4, §12).</summary>
public sealed class DietPlanRevision : PlanRevision
{
    private DietPlanRevision() { }

    public static DietPlanRevision Create(
        Guid planId, int versionNumber, PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate,
        Guid? createdByUserId, DateTimeOffset createdAt)
    {
        var revision = new DietPlanRevision();
        revision.Initialize(planId, versionNumber, status, startDate, endDate, createdByUserId, createdAt);
        return revision;
    }
}
