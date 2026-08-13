using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;

namespace CoachHub.Application.SavedPlans;

public sealed class SavedPlanQueryService(ISavedPlanQueryRepository repository)
{
    private static readonly HashSet<string> SortFields =
        ["name", "clientname", "type", "createdat", "weight", "calories", "protein", "carbohydrates", "fat", "workoutdays"];

    public Task<PagedResult<SavedPlanSummary>> ListAsync(
        SavedPlanQuery query, CancellationToken cancellationToken)
    {
        var normalized = query.Normalize();
        Validate(normalized);
        return repository.ListAsync(normalized, cancellationToken);
    }

    private static void Validate(SavedPlanQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        Range(query.MinCalories, query.MaxCalories, "calories", errors);
        Range(query.MinProtein, query.MaxProtein, "protein", errors);
        Range(query.MinCarbohydrates, query.MaxCarbohydrates, "carbohydrates", errors);
        Range(query.MinFat, query.MaxFat, "fat", errors);
        Range(query.MinWorkoutDays, query.MaxWorkoutDays, "workoutDays", errors);
        if (query.CreatedFrom > query.CreatedTo) errors["createdAt"] = ["Created-from cannot be after created-to."];
        var hasDietRange = query.MinCalories.HasValue || query.MaxCalories.HasValue ||
            query.MinProtein.HasValue || query.MaxProtein.HasValue ||
            query.MinCarbohydrates.HasValue || query.MaxCarbohydrates.HasValue ||
            query.MinFat.HasValue || query.MaxFat.HasValue;
        var hasWorkoutRange = query.MinWorkoutDays.HasValue || query.MaxWorkoutDays.HasValue;
        if (hasDietRange && hasWorkoutRange)
            errors["planType"] = ["Diet nutrition ranges and workout-day ranges cannot be combined."];
        if (query.PlanType == SavedPlanType.Workout && hasDietRange)
            errors["planType"] = ["Diet nutrition ranges require the Diet plan type."];
        if (query.PlanType == SavedPlanType.Diet && hasWorkoutRange)
            errors["planType"] = ["Workout-day ranges require the Workout plan type."];
        if (query.SortBy is not null && !SortFields.Contains(query.SortBy))
            errors["sortBy"] = ["Unsupported saved-plan sort field."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static void Range(decimal? minimum, decimal? maximum, string key, IDictionary<string, string[]> errors)
    {
        if (minimum < 0 || maximum < 0 || minimum > maximum)
            errors[key] = ["Range values must be non-negative and minimum cannot exceed maximum."];
    }
    private static void Range(int? minimum, int? maximum, string key, IDictionary<string, string[]> errors)
    {
        if (minimum < 0 || maximum < 0 || minimum > maximum)
            errors[key] = ["Range values must be non-negative and minimum cannot exceed maximum."];
    }
}
