using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

/// <summary>A version of a workout program (Workout V1 → V2, §59) with its program dates.</summary>
public sealed class WorkoutPlanRevision : PlanRevision
{
    private WorkoutPlanRevision() { }

    public static WorkoutPlanRevision Create(
        Guid planId, int versionNumber, PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate,
        Guid? createdByUserId, DateTimeOffset createdAt)
    {
        var revision = new WorkoutPlanRevision();
        revision.Initialize(planId, versionNumber, status, startDate, endDate, createdByUserId, createdAt);
        return revision;
    }
}
