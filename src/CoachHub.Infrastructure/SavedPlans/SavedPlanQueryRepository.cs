using CoachHub.Application.Common.Models;
using CoachHub.Application.SavedPlans;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.SavedPlans;

public sealed class SavedPlanQueryRepository(CoachHubDbContext dbContext) : ISavedPlanQueryRepository
{
    public async Task<PagedResult<SavedPlanSummary>> ListAsync(
        SavedPlanQuery query, CancellationToken cancellationToken)
    {
        var dietTotals =
            from version in dbContext.Set<DietPlanVersion>()
            join meal in dbContext.Set<Meal>() on version.Id equals meal.DietPlanVersionId
            join row in dbContext.Set<MealFoodItem>() on meal.Id equals row.MealId
            join food in dbContext.Set<FoodItem>() on row.FoodItemId equals food.Id
            group new { row, food } by version.DietPlanId into groupRows
            select new
            {
                PlanId = groupRows.Key,
                Weight = groupRows.Sum(x => x.row.Quantity),
                Calories = groupRows.Sum(x => x.food.CaloriesPer100 * x.row.Quantity / 100m),
                Protein = groupRows.Sum(x => x.food.ProteinPer100 * x.row.Quantity / 100m),
                Carbohydrates = groupRows.Sum(x => x.food.CarbohydratesPer100 * x.row.Quantity / 100m),
                Fat = groupRows.Sum(x => x.food.FatPer100 * x.row.Quantity / 100m)
            };

        var dietRows =
            from plan in dbContext.Set<DietPlan>()
            join clientValue in dbContext.Set<Client>() on plan.ClientId equals clientValue.Id into clients
            from client in clients.DefaultIfEmpty()
            join totalValue in dietTotals on plan.Id equals totalValue.PlanId into totals
            from total in totals.DefaultIfEmpty()
            select new
            {
                plan.Id, PlanType = SavedPlanType.Diet, plan.NameEn, plan.NameAr, plan.ClientId,
                ClientName = client == null ? null : client.Name,
                ClientCode = client == null ? null : client.ClientCode, plan.CreatedAt,
                TotalWeight = (decimal?)(total == null ? 0m : total.Weight),
                TotalCalories = (decimal?)(total == null ? 0m : total.Calories),
                TotalProtein = (decimal?)(total == null ? 0m : total.Protein),
                TotalCarbohydrates = (decimal?)(total == null ? 0m : total.Carbohydrates),
                TotalFat = (decimal?)(total == null ? 0m : total.Fat),
                WorkoutDayCount = (int?)null
            };

        var dayCounts =
            from day in dbContext.Set<WorkoutDay>()
            group day by day.WorkoutPlanId into days
            select new { PlanId = days.Key, Count = days.Count() };
        var workoutRows =
            from plan in dbContext.Set<WorkoutPlan>()
            join clientValue in dbContext.Set<Client>() on plan.ClientId equals clientValue.Id into clients
            from client in clients.DefaultIfEmpty()
            join countValue in dayCounts on plan.Id equals countValue.PlanId into counts
            from count in counts.DefaultIfEmpty()
            select new
            {
                plan.Id, PlanType = SavedPlanType.Workout, plan.NameEn, plan.NameAr, plan.ClientId,
                ClientName = client == null ? null : client.Name,
                ClientCode = client == null ? null : client.ClientCode, plan.CreatedAt,
                TotalWeight = (decimal?)null, TotalCalories = (decimal?)null,
                TotalProtein = (decimal?)null, TotalCarbohydrates = (decimal?)null,
                TotalFat = (decimal?)null, WorkoutDayCount = (int?)(count == null ? 0 : count.Count)
            };

        IQueryable<SavedPlanRow> rows = dietRows.Concat(workoutRows).Select(x => new SavedPlanRow(
            x.Id, x.PlanType, x.NameEn, x.NameAr, x.ClientId, x.ClientName, x.ClientCode,
            x.CreatedAt, x.TotalWeight, x.TotalCalories, x.TotalProtein,
            x.TotalCarbohydrates, x.TotalFat, x.WorkoutDayCount));
        rows = ApplyFilters(rows, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        rows = ApplySort(rows, query);
        var page = await rows.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .AsNoTracking().ToArrayAsync(cancellationToken);
        return new(page.Select(Map).ToArray(), query.PageNumber, query.PageSize, totalCount);
    }

    private static IQueryable<SavedPlanRow> ApplyFilters(IQueryable<SavedPlanRow> rows, SavedPlanQuery query)
    {
        if (query.PlanName is not null) rows = rows.Where(x => x.NameEn.Contains(query.PlanName) || (x.NameAr != null && x.NameAr.Contains(query.PlanName)));
        if (query.ClientName is not null) rows = rows.Where(x => x.ClientName != null && x.ClientName.Contains(query.ClientName));
        if (query.ClientCode is not null) rows = rows.Where(x => x.ClientCode == query.ClientCode);
        if (query.CreatedFrom.HasValue) rows = rows.Where(x => x.CreatedAt >= query.CreatedFrom.Value);
        if (query.CreatedTo.HasValue) rows = rows.Where(x => x.CreatedAt <= query.CreatedTo.Value);
        if (query.PlanType.HasValue) rows = rows.Where(x => x.PlanType == query.PlanType.Value);
        if (query.IsAssigned.HasValue) rows = query.IsAssigned.Value ? rows.Where(x => x.ClientId != null) : rows.Where(x => x.ClientId == null);
        if (query.MinCalories.HasValue) rows = rows.Where(x => x.TotalCalories >= query.MinCalories.Value);
        if (query.MaxCalories.HasValue) rows = rows.Where(x => x.TotalCalories <= query.MaxCalories.Value);
        if (query.MinProtein.HasValue) rows = rows.Where(x => x.TotalProtein >= query.MinProtein.Value);
        if (query.MaxProtein.HasValue) rows = rows.Where(x => x.TotalProtein <= query.MaxProtein.Value);
        if (query.MinCarbohydrates.HasValue) rows = rows.Where(x => x.TotalCarbohydrates >= query.MinCarbohydrates.Value);
        if (query.MaxCarbohydrates.HasValue) rows = rows.Where(x => x.TotalCarbohydrates <= query.MaxCarbohydrates.Value);
        if (query.MinFat.HasValue) rows = rows.Where(x => x.TotalFat >= query.MinFat.Value);
        if (query.MaxFat.HasValue) rows = rows.Where(x => x.TotalFat <= query.MaxFat.Value);
        if (query.MinWorkoutDays.HasValue) rows = rows.Where(x => x.WorkoutDayCount >= query.MinWorkoutDays.Value);
        if (query.MaxWorkoutDays.HasValue) rows = rows.Where(x => x.WorkoutDayCount <= query.MaxWorkoutDays.Value);
        return rows;
    }

    private static IQueryable<SavedPlanRow> ApplySort(IQueryable<SavedPlanRow> rows, SavedPlanQuery query)
    {
        var descending = query.SortBy is null || query.SortDescending;
        IOrderedQueryable<SavedPlanRow> ordered = query.SortBy switch
        {
            "name" => descending ? rows.OrderByDescending(x => x.NameEn) : rows.OrderBy(x => x.NameEn),
            "clientname" => descending ? rows.OrderByDescending(x => x.ClientName) : rows.OrderBy(x => x.ClientName),
            "type" => descending ? rows.OrderByDescending(x => x.PlanType) : rows.OrderBy(x => x.PlanType),
            "weight" => descending ? rows.OrderByDescending(x => x.TotalWeight) : rows.OrderBy(x => x.TotalWeight),
            "calories" => descending ? rows.OrderByDescending(x => x.TotalCalories) : rows.OrderBy(x => x.TotalCalories),
            "protein" => descending ? rows.OrderByDescending(x => x.TotalProtein) : rows.OrderBy(x => x.TotalProtein),
            "carbohydrates" => descending ? rows.OrderByDescending(x => x.TotalCarbohydrates) : rows.OrderBy(x => x.TotalCarbohydrates),
            "fat" => descending ? rows.OrderByDescending(x => x.TotalFat) : rows.OrderBy(x => x.TotalFat),
            "workoutdays" => descending ? rows.OrderByDescending(x => x.WorkoutDayCount) : rows.OrderBy(x => x.WorkoutDayCount),
            _ => descending ? rows.OrderByDescending(x => x.CreatedAt) : rows.OrderBy(x => x.CreatedAt)
        };
        return ordered.ThenBy(x => x.Id);
    }

    private static SavedPlanSummary Map(SavedPlanRow row) => new(
        row.Id, row.PlanType, row.NameEn, row.NameAr, row.ClientId, row.ClientName, row.ClientCode,
        row.CreatedAt, Round(row.TotalWeight), Round(row.TotalCalories), Round(row.TotalProtein),
        Round(row.TotalCarbohydrates), Round(row.TotalFat), row.WorkoutDayCount);
    private static decimal? Round(decimal? value) => value.HasValue ? decimal.Round(value.Value, 2) : null;

    private sealed record SavedPlanRow(
        Guid Id, SavedPlanType PlanType, string NameEn, string? NameAr, Guid? ClientId,
        string? ClientName, string? ClientCode, DateTimeOffset CreatedAt,
        decimal? TotalWeight, decimal? TotalCalories, decimal? TotalProtein,
        decimal? TotalCarbohydrates, decimal? TotalFat, int? WorkoutDayCount);
}
