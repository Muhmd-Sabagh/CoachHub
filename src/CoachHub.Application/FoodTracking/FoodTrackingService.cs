using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.DietPlanning;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.FoodTracking;

/// <summary>
/// The client's food diary (§8) against the diet in force (§4, §7, §13), and planned versus actual (§9, §10, §11).
/// Clients write their own diary; coaches read their clients'. Tenant filters decide who sees which client.
/// </summary>
public sealed class FoodTrackingService(
    IFoodTrackingRepository repository, IDietPlanRepository diets, DietPlanService dietPlans, TimeProvider timeProvider)
{
    /// <summary>The longest analytics range, and how far back the diary can be written.</summary>
    public const int MaximumRangeDays = 366;

    public async Task<NutritionDayResponse> GetDayAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        return Map(await DayAsync(clientId, date, cancellationToken));
    }

    /// <summary>The alternatives the coach permits for a planned food of the day (§7), with their quantities.</summary>
    public async Task<IReadOnlyList<FoodAlternativeResponse>> AlternativesAsync(
        Guid clientId, DateOnly date, Guid plannedFoodId, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var planned = (await DayAsync(clientId, date, cancellationToken)).Planned
            ?? throw new NotFoundException("Planned food", plannedFoodId);
        var row = planned.Row(plannedFoodId) ?? throw new NotFoundException("Planned food", plannedFoodId);
        return planned.AlternativesFor(row.Id).Select(x =>
        {
            var (group, option) = x;
            var food = planned.Figures.Food(option.ReplacementFoodItemId!.Value);
            var (label, labelAr) = DietPlanService.UnitLabels(food, option.FoodUnitId);
            var (quantity, calculated) = planned.Figures.Quantity(option);
            return new FoodAlternativeResponse(option.Id, group.Id, group.Title, group.Mode, food.Id, food.NameEn, food.NameAr,
                quantity, option.FoodUnitId, label, labelAr, calculated,
                MacroAmounts.From(planned.Figures.Alternative(option)), option.Notes);
        }).ToArray();
    }

    /// <summary>Coach nutrition analytics (§11): each day, each week and each month of the range.</summary>
    public async Task<NutritionAnalyticsResponse> AnalyticsAsync(
        Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw Validation("to", "The end date cannot be before the start date.");
        if (to.DayNumber - from.DayNumber >= MaximumRangeDays) throw Validation("to", $"A range covers at most {MaximumRangeDays} days.");
        await ClientCoachAsync(clientId, cancellationToken);
        var today = Today();
        var versions = await repository.ListDietVersionsAsync(clientId, cancellationToken);
        var days = (await repository.ListDaysAsync(clientId, from, to, cancellationToken)).ToDictionary(x => x.Date);
        var entries = (await repository.ListEntriesAsync(clientId, from, to, cancellationToken)).ToLookup(x => x.Date);
        var cache = new Dictionary<(Guid, int), PlannedVersion?>();
        var rows = new List<NutritionDayRow>();
        for (var date = from; date <= to && date <= today; date = date.AddDays(1))
        {
            var day = days.GetValueOrDefault(date);
            var planned = await PlannedAsync(versions, date, day, cache, cancellationToken);
            var summary = Summarize(planned, day, entries[date].ToArray());
            rows.Add(new NutritionDayRow(date, summary.Target, summary.Planned, summary.Consumed, summary.AdherencePercent,
                summary.MealCompletionPercent, summary.EntryCount, summary.IsCompleted));
        }
        var weeks = rows.GroupBy(x => NutritionAdherence.WeekStart(x.Date)).Select(week => NutritionAdherence.Summarize(
            Max(week.Key, from), Min(week.Key.AddDays(6), to), week, today)).ToArray();
        var months = rows.GroupBy(x => new DateOnly(x.Date.Year, x.Date.Month, 1)).Select(month => NutritionAdherence.Summarize(
            Max(month.Key, from), Min(month.Key.AddMonths(1).AddDays(-1), to), month, today)).ToArray();
        return new NutritionAnalyticsResponse(from, to, NutritionAdherence.Summarize(from, to, rows, today), rows, weeks, months);
    }

    /// <summary>Chooses the day's variant (null keeps it) and options of flexible meals (null goes back to the first).</summary>
    public async Task<NutritionDayResponse> ChooseAsync(
        Guid clientId, DateOnly date, NutritionDayChoicesInput input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        var context = await DayAsync(clientId, date, cancellationToken);
        var planned = context.Planned ?? throw Validation("date", "No diet plan applies to this day.");
        if (input.VariantId is { } variantId && planned.Version.Variants.All(x => x.Id != variantId))
            throw Validation("variantId", "The variant is not part of the day's plan.");
        var meals = planned.Version.Meals.Where(x => x.DietPlanVariantId == (input.VariantId ?? planned.Variant.Id)).Select(x => x.Id).ToHashSet();
        foreach (var choice in input.MealOptions ?? [])
        {
            if (!meals.Contains(choice.MealId)) throw Validation("mealOptions", "A meal is not part of the day's plan.");
            if (choice.OptionId is { } optionId && planned.Figures.OptionsOf(choice.MealId).All(x => x.Id != optionId))
                throw Validation("mealOptions", "An option does not belong to its meal.");
        }

        var now = timeProvider.GetUtcNow();
        var day = context.Day;
        if (day is null)
        {
            day = NutritionDay.Create(coachId, clientId, date, now);
            repository.Add(day);
        }
        DomainValidation.Run(() =>
        {
            if (input.VariantId.HasValue) day.ChooseVariant(input.VariantId, now);
            foreach (var choice in input.MealOptions ?? []) day.ChooseOption(choice.MealId, choice.OptionId, now);
        }, "choices");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(await DayAsync(clientId, date, cancellationToken));
    }

    /// <summary>Logs a food (§8): a planned one, an alternative eaten instead (§7), a catalog extra, or a custom food.</summary>
    public async Task<FoodLogEntryResponse> LogAsync(Guid clientId, FoodLogEntryInput input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(input.Date);
        if (!Enum.IsDefined(input.Slot)) throw Validation("slot", "Unknown meal slot.");
        var now = timeProvider.GetUtcNow();
        FoodLogEntry entry;
        if (input.Custom is { } custom)
        {
            if (input.FoodItemId.HasValue || input.PlannedMealFoodItemId.HasValue || input.ReplacementOptionId.HasValue ||
                input.Quantity.HasValue || input.FoodUnitId.HasValue)
                throw Validation("custom", "A custom food stands alone: leave out the catalog and planned food.");
            entry = DomainValidation.Run(() => FoodLogEntry.Custom(coachId, clientId, input.Date, input.Slot, custom.NameEn,
                Nutrients(custom), now), "custom");
        }
        else if (input.PlannedMealFoodItemId is { } rowId)
        {
            var planned = (await DayAsync(clientId, input.Date, cancellationToken)).Planned
                ?? throw Validation("plannedMealFoodItemId", "No diet plan applies to this day.");
            var row = planned.Row(rowId) ?? throw Validation("plannedMealFoodItemId", "The food is not part of the day's plan.");
            if (input.ReplacementOptionId is { } optionId)
            {
                var option = planned.AlternativesFor(row.Id).Select(x => x.Option).SingleOrDefault(x => x.Id == optionId)
                    ?? throw Validation("replacementOptionId", "The coach does not permit this alternative for the food.");
                var food = planned.Figures.Food(option.ReplacementFoodItemId!.Value);
                if (input.FoodItemId is { } chosen && chosen != food.Id) throw Validation("foodItemId", "The food does not match the alternative.");
                var (quantity, unit) = input.Quantity is { } entered
                    ? (entered, input.FoodUnitId)
                    : (planned.Figures.Quantity(option).Quantity ?? throw Validation("quantity", "Enter how much you ate."), option.FoodUnitId);
                entry = Entry(coachId, clientId, input.Date, input.Slot, food, quantity, unit,
                    new PlannedFoodReference(row.MealId, row.Id, option.Id), now);
            }
            else
            {
                var food = planned.Figures.Food(row.FoodItemId);
                if (input.FoodItemId is { } chosen && chosen != food.Id) throw Validation("foodItemId", "The food does not match the planned food.");
                var (quantity, unit) = input.Quantity is { } entered ? (entered, input.FoodUnitId) : (row.Quantity, row.FoodUnitId);
                entry = Entry(coachId, clientId, input.Date, input.Slot, food, quantity, unit,
                    new PlannedFoodReference(row.MealId, row.Id), now);
            }
        }
        else if (input.FoodItemId is { } foodId)
        {
            if (input.ReplacementOptionId.HasValue) throw Validation("replacementOptionId", "An alternative replaces a planned food.");
            var food = (await diets.FindFoodsAsync([foodId], cancellationToken)).GetValueOrDefault(foodId)
                ?? throw Validation("foodItemId", "The food does not exist.");
            var quantity = input.Quantity ?? throw Validation("quantity", "Enter how much you ate.");
            entry = Entry(coachId, clientId, input.Date, input.Slot, food, quantity, input.FoodUnitId, null, now);
        }
        else
        {
            throw Validation("foodItemId", "Choose a food, a planned food or enter a custom food.");
        }

        repository.Add([entry]);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    /// <summary>Logs a whole planned meal (§8): every food of its chosen option, as planned.</summary>
    public async Task<NutritionDayResponse> LogMealAsync(Guid clientId, DateOnly date, Guid mealId, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        var context = await DayAsync(clientId, date, cancellationToken);
        var planned = context.Planned ?? throw new NotFoundException("Planned meal", mealId);
        var meal = planned.Meals.SingleOrDefault(x => x.Id == mealId) ?? throw new NotFoundException("Planned meal", mealId);
        if (context.Entries.Any(x => x.PlannedMealId == meal.Id))
            throw new ConflictException("This meal is already logged. Edit or delete its foods instead.");
        var rows = planned.Figures.RowsOf(meal.Id, context.Day?.OptionFor(meal.Id));
        if (rows.Count == 0) throw Validation("mealId", "The meal has no foods to log.");
        var now = timeProvider.GetUtcNow();
        repository.Add(rows.Select(row => Entry(coachId, clientId, date, meal.Type, planned.Figures.Food(row.FoodItemId),
            row.Quantity, row.FoodUnitId, new PlannedFoodReference(meal.Id, row.Id), now)).ToArray());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(await DayAsync(clientId, date, cancellationToken));
    }

    public async Task<FoodLogEntryResponse> UpdateAsync(
        Guid clientId, Guid entryId, FoodLogEntryUpdate input, CancellationToken cancellationToken)
    {
        var entry = await RequiredEntryAsync(clientId, entryId, cancellationToken);
        EnsureWritable(entry.Date);
        var now = timeProvider.GetUtcNow();
        if (input.Slot is { } slot)
        {
            if (!Enum.IsDefined(slot)) throw Validation("slot", "Unknown meal slot.");
            entry.MoveTo(slot, now);
        }
        if (entry.IsCustom)
        {
            if (input.Quantity.HasValue || input.FoodUnitId.HasValue) throw Validation("quantity", "A custom food has no quantity; change its nutrients.");
            if (input.Custom is { } custom) DomainValidation.Run(() => entry.ChangeCustom(custom.NameEn, Nutrients(custom), now), "custom");
        }
        else
        {
            if (input.Custom is not null) throw Validation("custom", "Only a custom food has entered nutrients.");
            if (input.Quantity is { } quantity)
            {
                // The entry is visible, so its food is readable even if a previous coach owns it.
                var food = (await diets.FindReferencedFoodsAsync([entry.FoodItemId!.Value], cancellationToken))[entry.FoodItemId.Value];
                var (portion, nutrients) = Portion(food, quantity, input.FoodUnitId);
                DomainValidation.Run(() => entry.ChangePortion(portion, nutrients, now), "quantity");
            }
        }
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task DeleteAsync(Guid clientId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await RequiredEntryAsync(clientId, entryId, cancellationToken);
        EnsureWritable(entry.Date);
        repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<DayContext> DayAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken)
    {
        var versions = await repository.ListDietVersionsAsync(clientId, cancellationToken);
        var day = await repository.FindDayAsync(clientId, date, cancellationToken);
        var entries = await repository.ListEntriesAsync(clientId, date, date, cancellationToken);
        var planned = await PlannedAsync(versions, date, day, [], cancellationToken);
        return new DayContext(date, day, planned, entries);
    }

    /// <summary>The diet in force on the day, its figures, and the variant the client follows (by default the first).</summary>
    private async Task<PlannedDay?> PlannedAsync(
        IReadOnlyList<DietVersion> versions, DateOnly date, NutritionDay? day,
        Dictionary<(Guid, int), PlannedVersion?> cache, CancellationToken cancellationToken)
    {
        if (NutritionAdherence.InForce(versions, date) is not { } inForce) return null;
        var key = (inForce.Plan.Id, inForce.Revision.VersionNumber);
        if (!cache.TryGetValue(key, out var version))
        {
            var aggregate = await diets.FindAsync(inForce.Plan.Id, inForce.Revision.VersionNumber, false, cancellationToken);
            version = aggregate is null || aggregate.Variants.Count == 0
                ? null
                : new PlannedVersion(aggregate, await dietPlans.FiguresAsync(aggregate, cancellationToken));
            cache[key] = version;
        }
        if (version is null) return null;
        var variant = version.Aggregate.Variants.SingleOrDefault(x => x.Id == day?.VariantId)
            ?? version.Aggregate.Variants.OrderBy(x => x.Order).First();
        var meals = version.Aggregate.Meals.Where(x => x.DietPlanVariantId == variant.Id).OrderBy(x => x.Order).ToArray();
        return new PlannedDay(version.Aggregate, version.Figures, variant, meals);
    }

    private static NutritionDaySummary Summarize(PlannedDay? planned, NutritionDay? day, IReadOnlyList<FoodLogEntry> entries)
    {
        var consumed = entries.Aggregate(MacroAmounts.Zero, (sum, x) => sum + MacroAmounts.From(x.Nutrients)).Rounded();
        if (planned is null) return new(null, null, consumed, null, 0, 0, null, null, entries.Count, false);
        var plannedTotals = planned.Meals.Aggregate(MacroAmounts.Zero,
            (sum, meal) => sum + MacroAmounts.From(planned.Figures.Meal(meal.Id, day?.OptionFor(meal.Id)))).Rounded();
        var targets = planned.Version.Revision.Targets;
        var target = new MacroAmounts(
            targets.Calories ?? plannedTotals.Calories, targets.Protein ?? plannedTotals.Protein,
            targets.Carbohydrates ?? plannedTotals.Carbohydrates, targets.Fat ?? plannedTotals.Fat, plannedTotals.Fiber);
        var completed = planned.Meals.Count(meal => entries.Any(x => x.PlannedMealId == meal.Id));
        return new(target, plannedTotals, consumed, (target - consumed).Rounded(), planned.Meals.Count, completed,
            NutritionAdherence.Share(completed, planned.Meals.Count),
            entries.Count == 0 ? null : NutritionAdherence.Percent(target, consumed),
            entries.Count, planned.Meals.Count > 0 && completed == planned.Meals.Count);
    }

    private static NutritionDayResponse Map(DayContext context)
    {
        var summary = Summarize(context.Planned, context.Day, context.Entries);
        PlannedDayResponse? plan = null;
        if (context.Planned is { } planned)
        {
            var aggregate = planned.Version;
            var meals = planned.Meals.Select(meal =>
            {
                var chosen = planned.Figures.OptionFor(meal.Id, context.Day?.OptionFor(meal.Id));
                return new PlannedMealResponse(meal.Id, meal.NameEn, meal.NameAr, meal.Type, meal.Notes,
                    planned.Figures.OptionsOf(meal.Id).Select(option => new PlannedMealOptionResponse(
                        option.Id, option.NameEn, option.NameAr, MacroAmounts.From(planned.Figures.Option(option.Id)))).ToArray(),
                    chosen,
                    planned.Figures.RowsOf(meal.Id, chosen).Select(row =>
                    {
                        var food = planned.Figures.Food(row.FoodItemId);
                        var (label, labelAr) = DietPlanService.UnitLabels(food, row.FoodUnitId);
                        return new PlannedFoodResponse(row.Id, food.Id, food.NameEn, food.NameAr, row.Quantity, row.FoodUnitId,
                            label, labelAr, MacroAmounts.From(planned.Figures.Row(row.Id)), planned.AlternativesFor(row.Id).Count, row.Notes);
                    }).ToArray(),
                    MacroAmounts.From(planned.Figures.Meal(meal.Id, chosen)),
                    context.Entries.Any(x => x.PlannedMealId == meal.Id));
            }).ToArray();
            plan = new PlannedDayResponse(aggregate.Plan.Id, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
                aggregate.Revision.VersionNumber, planned.Variant.Id,
                aggregate.Variants.OrderBy(x => x.Order).Select(x => new PlannedVariantResponse(x.Id, x.NameEn, x.NameAr)).ToArray(),
                meals);
        }
        return new NutritionDayResponse(context.Date, plan,
            context.Entries.OrderBy(x => x.Slot).ThenBy(x => x.LoggedAt).Select(Map).ToArray(), summary);
    }

    private static FoodLogEntryResponse Map(FoodLogEntry entry) => new(
        entry.Id, entry.Date, entry.Slot, entry.Source, entry.NameEn, entry.NameAr, entry.IsCustom,
        entry.FoodItemId, entry.Quantity, entry.FoodUnitId, entry.UnitLabel, entry.UnitLabelAr,
        MacroAmounts.From(entry.Nutrients), entry.PlannedMealId, entry.PlannedMealFoodItemId, entry.ReplacementOptionId,
        entry.LoggedAt);

    private static FoodLogEntry Entry(
        Guid coachId, Guid clientId, DateOnly date, MealType slot, FoodItem food, decimal quantity, Guid? unitId,
        PlannedFoodReference? planned, DateTimeOffset now)
    {
        var (portion, nutrients) = Portion(food, quantity, unitId);
        return DomainValidation.Run(() => FoodLogEntry.ForFood(
            coachId, clientId, date, slot, food.NameEn, food.NameAr, portion, nutrients, planned, now), "foodLogEntry");
    }

    /// <summary>A quantity of a food as eaten, with its nutrients from the food's current values.</summary>
    private static (FoodLogPortion Portion, FoodLogNutrients Nutrients) Portion(FoodItem food, decimal quantity, Guid? unitId)
    {
        if (quantity <= 0 || quantity > 10000m) throw Validation("quantity", "The quantity must be above 0 and at most 10000.");
        if (unitId is not null && food.FindUnit(unitId) is null) throw Validation("foodUnitId", "The unit does not belong to the food.");
        var totals = NutritionCalculator.Calculate(food, quantity, unitId);
        var (label, labelAr) = DietPlanService.UnitLabels(food, unitId);
        return (new FoodLogPortion(food.Id, unitId, quantity, totals.Weight, label, labelAr),
            new FoodLogNutrients(totals.Calories, totals.Protein, totals.Carbohydrates, totals.Fat, totals.Fiber));
    }

    private static FoodLogNutrients Nutrients(CustomFoodInput custom) =>
        new(custom.Calories, custom.Protein, custom.Carbohydrates, custom.Fat, custom.Fiber);

    private async Task<Guid> ClientCoachAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientCoachIdAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private async Task<FoodLogEntry> RequiredEntryAsync(Guid clientId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await repository.FindEntryAsync(entryId, cancellationToken);
        return entry is not null && entry.ClientId == clientId ? entry : throw new NotFoundException("Food log entry", entryId);
    }

    /// <summary>The diary takes days from a year back up to tomorrow (UTC), which covers every time zone's today.</summary>
    private void EnsureWritable(DateOnly date)
    {
        var today = Today();
        if (date > today.AddDays(1)) throw Validation("date", "Food cannot be logged for a future day.");
        if (date < today.AddDays(-MaximumRangeDays)) throw Validation("date", "The day is too far back to change.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });

    private sealed record DayContext(DateOnly Date, NutritionDay? Day, PlannedDay? Planned, IReadOnlyList<FoodLogEntry> Entries);
    private sealed record PlannedVersion(DietPlanAggregate Aggregate, DietPlanFigures Figures);

    /// <summary>The diet in force on a day: its version, figures, the variant followed and that variant's meals.</summary>
    private sealed record PlannedDay(DietPlanAggregate Version, DietPlanFigures Figures, DietPlanVariant Variant, IReadOnlyList<Meal> Meals)
    {
        /// <summary>A food row of one of the day's meals (of any of its options).</summary>
        public MealFoodItem? Row(Guid rowId)
        {
            var mealIds = Meals.Select(x => x.Id).ToHashSet();
            return Version.FoodItems.SingleOrDefault(x => x.Id == rowId && mealIds.Contains(x.MealId));
        }

        /// <summary>The food alternatives the coach permits for a row, in order (§7).</summary>
        public IReadOnlyList<(DietReplacementGroup Group, DietReplacementOption Option)> AlternativesFor(Guid rowId) =>
            Version.ReplacementGroups.Where(x => x.TargetMealFoodItemId == rowId).OrderBy(x => x.Order)
                .SelectMany(group => Version.ReplacementOptions
                    .Where(x => x.DietReplacementGroupId == group.Id && x.ReplacementFoodItemId.HasValue)
                    .OrderBy(x => x.Order).Select(option => (group, option)))
                .ToArray();
    }
}
