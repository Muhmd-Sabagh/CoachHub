using CoachHub.Application.Common.Models;

namespace CoachHub.Application.SavedPlans;

public enum SavedPlanType { Diet, Workout }

public sealed record SavedPlanQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? PlanName { get; init; }
    public string? ClientName { get; init; }
    public string? ClientCode { get; init; }
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public SavedPlanType? PlanType { get; init; }
    public decimal? MinCalories { get; init; }
    public decimal? MaxCalories { get; init; }
    public decimal? MinProtein { get; init; }
    public decimal? MaxProtein { get; init; }
    public decimal? MinCarbohydrates { get; init; }
    public decimal? MaxCarbohydrates { get; init; }
    public decimal? MinFat { get; init; }
    public decimal? MaxFat { get; init; }
    public int? MinWorkoutDays { get; init; }
    public int? MaxWorkoutDays { get; init; }
    public bool? IsAssigned { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;

    public SavedPlanQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber), PageSize = Math.Clamp(PageSize, 1, 100),
        PlanName = Clean(PlanName), ClientName = Clean(ClientName), ClientCode = Clean(ClientCode),
        SortBy = Clean(SortBy)?.ToLowerInvariant()
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SavedPlanSummary(
    Guid Id, SavedPlanType PlanType, string NameEn, string? NameAr,
    Guid? ClientId, string? ClientName, string? ClientCode, DateTimeOffset CreatedAt,
    decimal? TotalWeight, decimal? TotalCalories, decimal? TotalProtein,
    decimal? TotalCarbohydrates, decimal? TotalFat, int? WorkoutDayCount);

public interface ISavedPlanQueryRepository
{
    Task<PagedResult<SavedPlanSummary>> ListAsync(SavedPlanQuery query, CancellationToken cancellationToken);
}
