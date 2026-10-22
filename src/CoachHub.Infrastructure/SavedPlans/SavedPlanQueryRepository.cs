using CoachHub.Application.Common.Models;
using CoachHub.Application.SavedPlans;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;
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
        // Lists show each plan as its current version (D4); archived versions are history.
        // The projections below avoid "x == null ? a : b" over left-joined rows: SQL Server cannot
        // run the Concat once EF evaluates such a projection on the client. Missing rows yield NULL.
        var currentDiets = dbContext.Set<DietPlanRevision>().Where(x => x.Status != PlanRevisionStatus.Archived);
        // Rows convert their unit to base units (P7). Foods stay tenant-filtered: IgnoreQueryFilters would
        // lift the filters of the whole query, plans included. A transferred client's plan that still uses the
        // previous coach's custom food therefore lists without that food (its detail view includes it).
        // A meal with options (P8) counts its first option, like the plan's own totals.
        var dietTotals =
            from revision in currentDiets
            join row in dbContext.Set<MealFoodItem>() on revision.Id equals row.RevisionId
            where row.MealOptionId == null || row.MealOptionId == dbContext.Set<MealOption>()
                .Where(option => option.MealId == row.MealId).OrderBy(option => option.Order)
                .Select(option => (Guid?)option.Id).FirstOrDefault()
            join food in dbContext.Set<FoodItem>() on row.FoodItemId equals food.Id
            join unitValue in dbContext.Set<FoodUnit>() on row.FoodUnitId equals (Guid?)unitValue.Id into units
            from unit in units.DefaultIfEmpty()
            let amount = row.Quantity * ((decimal?)unit!.BaseQuantity ?? 1m)
            group new { amount, food } by revision.PlanId into groupRows
            select new
            {
                PlanId = groupRows.Key,
                Weight = groupRows.Sum(x => x.amount),
                Calories = groupRows.Sum(x => x.food.CaloriesPer100 * x.amount / 100m),
                Protein = groupRows.Sum(x => x.food.ProteinPer100 * x.amount / 100m),
                Carbohydrates = groupRows.Sum(x => x.food.CarbohydratesPer100 * x.amount / 100m),
                Fat = groupRows.Sum(x => x.food.FatPer100 * x.amount / 100m)
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
                ClientName = (string?)client!.Name, plan.CreatedAt,
                TotalWeight = (decimal?)((decimal?)total!.Weight ?? 0m),
                TotalCalories = (decimal?)((decimal?)total.Calories ?? 0m),
                TotalProtein = (decimal?)((decimal?)total.Protein ?? 0m),
                TotalCarbohydrates = (decimal?)((decimal?)total.Carbohydrates ?? 0m),
                TotalFat = (decimal?)((decimal?)total.Fat ?? 0m),
                WorkoutDayCount = (int?)null
            };

        var currentWorkouts = dbContext.Set<WorkoutPlanRevision>().Where(x => x.Status != PlanRevisionStatus.Archived);
        var dayCounts =
            from revision in currentWorkouts
            join day in dbContext.Set<WorkoutDay>() on revision.Id equals day.RevisionId
            group day by revision.PlanId into days
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
                ClientName = (string?)client!.Name, plan.CreatedAt,
                TotalWeight = (decimal?)null, TotalCalories = (decimal?)null,
                TotalProtein = (decimal?)null, TotalCarbohydrates = (decimal?)null,
                TotalFat = (decimal?)null, WorkoutDayCount = (int?)((int?)count!.Count ?? 0)
            };

        // A member-init projection (not a constructor), so filters and sorts on it translate to SQL.
        IQueryable<SavedPlanRow> rows = dietRows.Concat(workoutRows).Select(x => new SavedPlanRow
        {
            Id = x.Id, PlanType = x.PlanType, NameEn = x.NameEn, NameAr = x.NameAr, ClientId = x.ClientId,
            ClientName = x.ClientName, CreatedAt = x.CreatedAt,
            TotalWeight = x.TotalWeight, TotalCalories = x.TotalCalories, TotalProtein = x.TotalProtein,
            TotalCarbohydrates = x.TotalCarbohydrates, TotalFat = x.TotalFat, WorkoutDayCount = x.WorkoutDayCount
        });
        rows = ApplyFilters(rows, query);
        var totalCount = await rows.CountAsync(cancellationToken);
        rows = ApplySort(rows, query);
        var page = await rows.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .AsNoTracking().ToArrayAsync(cancellationToken);
        var versions = await CurrentVersionsAsync(page, cancellationToken);
        return new(page.Select(row => Map(row, versions.GetValueOrDefault(row.Id))).ToArray(),
            query.PageNumber, query.PageSize, totalCount);
    }

    /// <summary>The current version (number, status, program dates) of each listed plan.</summary>
    private async Task<Dictionary<Guid, CurrentVersion>> CurrentVersionsAsync(
        IReadOnlyCollection<SavedPlanRow> page, CancellationToken cancellationToken)
    {
        var dietIds = page.Where(x => x.PlanType == SavedPlanType.Diet).Select(x => x.Id).ToArray();
        var workoutIds = page.Where(x => x.PlanType == SavedPlanType.Workout).Select(x => x.Id).ToArray();
        var diets = await dbContext.Set<DietPlanRevision>().AsNoTracking()
            .Where(x => dietIds.Contains(x.PlanId) && x.Status != PlanRevisionStatus.Archived)
            .Select(x => new CurrentVersion(x.PlanId, x.VersionNumber, x.Status, x.StartDate, x.EndDate))
            .ToListAsync(cancellationToken);
        var workouts = await dbContext.Set<WorkoutPlanRevision>().AsNoTracking()
            .Where(x => workoutIds.Contains(x.PlanId) && x.Status != PlanRevisionStatus.Archived)
            .Select(x => new CurrentVersion(x.PlanId, x.VersionNumber, x.Status, x.StartDate, x.EndDate))
            .ToListAsync(cancellationToken);
        return diets.Concat(workouts).ToDictionary(x => x.PlanId);
    }

    private static IQueryable<SavedPlanRow> ApplyFilters(IQueryable<SavedPlanRow> rows, SavedPlanQuery query)
    {
        if (query.PlanName is not null) rows = rows.Where(x => x.NameEn.Contains(query.PlanName) || (x.NameAr != null && x.NameAr.Contains(query.PlanName)));
        if (query.ClientName is not null) rows = rows.Where(x => x.ClientName != null && x.ClientName.Contains(query.ClientName));
        if (query.ClientId.HasValue) rows = rows.Where(x => x.ClientId == query.ClientId.Value);
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

    private static SavedPlanSummary Map(SavedPlanRow row, CurrentVersion? version) => new(
        row.Id, row.PlanType, row.NameEn, row.NameAr, row.ClientId, row.ClientName,
        row.CreatedAt, Round(row.TotalWeight), Round(row.TotalCalories), Round(row.TotalProtein),
        Round(row.TotalCarbohydrates), Round(row.TotalFat), row.WorkoutDayCount,
        version?.VersionNumber, version?.Status, version?.StartDate, version?.EndDate);
    private static decimal? Round(decimal? value) => value.HasValue ? decimal.Round(value.Value, 2) : null;

    private sealed class SavedPlanRow
    {
        public Guid Id { get; init; }
        public SavedPlanType PlanType { get; init; }
        public string NameEn { get; init; } = string.Empty;
        public string? NameAr { get; init; }
        public Guid? ClientId { get; init; }
        public string? ClientName { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public decimal? TotalWeight { get; init; }
        public decimal? TotalCalories { get; init; }
        public decimal? TotalProtein { get; init; }
        public decimal? TotalCarbohydrates { get; init; }
        public decimal? TotalFat { get; init; }
        public int? WorkoutDayCount { get; init; }
    }

    private sealed record CurrentVersion(
        Guid PlanId, int VersionNumber, PlanRevisionStatus Status, DateOnly? StartDate, DateOnly? EndDate);
}
