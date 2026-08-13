using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;

namespace CoachHub.Application.DietPlanning;

public sealed class DietPlanService(IDietPlanRepository repository, TimeProvider timeProvider)
{
    public async Task<DietPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, false, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<DietPlanResponse> CreateAsync(DietPlanInput input, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(input.ClientId, cancellationToken);
        var plan = CreatePlan(input.NameEn, input.NameAr, input.ClientId);
        var aggregate = Build(plan, input);
        await ValidateReferencesAsync(aggregate, cancellationToken);
        await repository.AddAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<DietPlanResponse> UpdateAsync(
        Guid id, DietPlanInput input, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(input.ClientId, cancellationToken);
        var existing = await RequiredAsync(id, true, cancellationToken);
        TryDomain(() => existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId));
        var aggregate = Build(existing.Plan, input);
        await ValidateReferencesAsync(aggregate, cancellationToken);
        await repository.ReplaceChildrenAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<DietPlanResponse> CopyAsync(
        Guid id, CopyDietPlanInput input, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(input.ClientId, cancellationToken);
        var source = await RequiredAsync(id, false, cancellationToken);
        var versionIds = source.Versions.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var mealIds = source.Meals.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var itemIds = source.FoodItems.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var groupIds = source.ReplacementGroups.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var copyInput = new DietPlanInput(
            input.NameEn, input.NameAr, input.ClientId,
            source.Notes.Select(x => new DietPlanNoteInput(Guid.NewGuid(), x.Text, x.Order, x.IsActive)).ToArray(),
            source.Versions.Select(version => new DietPlanVersionInput(
                versionIds[version.Id], version.NameEn, version.NameAr, version.Order,
                version.IsActiveForPdf, version.Notes,
                source.Meals.Where(x => x.DietPlanVersionId == version.Id).Select(meal => new MealInput(
                    mealIds[meal.Id], meal.NameEn, meal.NameAr, meal.Order, meal.Notes,
                    source.FoodItems.Where(x => x.MealId == meal.Id).Select(item =>
                        new MealFoodItemInput(itemIds[item.Id], item.FoodItemId, item.Quantity, item.Order, item.Notes)).ToArray())).ToArray(),
                source.ReplacementGroups.Where(x => x.DietPlanVersionId == version.Id).Select(group =>
                    new DietReplacementGroupInput(
                        groupIds[group.Id], mealIds[group.TargetMealId],
                        group.TargetMealFoodItemId.HasValue ? itemIds[group.TargetMealFoodItemId.Value] : null,
                        group.Title, group.Order,
                        source.ReplacementOptions.Where(x => x.DietReplacementGroupId == group.Id).Select(option =>
                            new DietReplacementOptionInput(
                                Guid.NewGuid(), option.ReplacementFoodItemId,
                                option.ReplacementMealId.HasValue ? mealIds[option.ReplacementMealId.Value] : null,
                                option.Quantity, option.Order, option.Notes)).ToArray())).ToArray())).ToArray());
        return await CreateAsync(copyInput, cancellationToken);
    }

    public async Task<DietPlanResponse> AssignAsync(
        Guid id, Guid? clientId, CancellationToken cancellationToken)
    {
        await ValidateClientAsync(clientId, cancellationToken);
        var aggregate = await RequiredAsync(id, true, cancellationToken);
        TryDomain(() => aggregate.Plan.Assign(clientId));
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task<DietPlanResponse> SetNoteActiveAsync(
        Guid id, Guid noteId, bool isActive, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, true, cancellationToken);
        var note = aggregate.Notes.SingleOrDefault(x => x.Id == noteId)
            ?? throw new NotFoundException("Diet plan note", noteId);
        note.SetActive(isActive);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(await RequiredAsync(id, true, cancellationToken), cancellationToken);

    private DietPlan CreatePlan(string nameEn, string? nameAr, Guid? clientId)
    {
        DietPlan? plan = null;
        TryDomain(() => plan = DietPlan.Create(nameEn, nameAr, clientId, timeProvider.GetUtcNow()));
        return plan!;
    }

    private static DietPlanAggregate Build(DietPlan plan, DietPlanInput input)
    {
        var errors = ValidateShape(input);
        if (errors.Count > 0) throw new ValidationException(errors);
        try
        {
            var notes = input.Notes.Select(x => DietPlanNote.Create(x.Id, plan.Id, x.Text, x.Order, x.IsActive)).ToArray();
            var versions = input.Versions.Select(x => DietPlanVersion.Create(
                x.Id, plan.Id, x.NameEn, x.NameAr, x.Order, x.IsActiveForPdf, x.Notes)).ToArray();
            var meals = input.Versions.SelectMany(version => version.Meals.Select(x => Meal.Create(
                x.Id, version.Id, x.NameEn, x.NameAr, x.Order, x.Notes))).ToArray();
            var foods = input.Versions.SelectMany(v => v.Meals.SelectMany(meal => meal.FoodItems.Select(x =>
                MealFoodItem.Create(x.Id, meal.Id, x.FoodItemId, x.Quantity, x.Order, x.Notes)))).ToArray();
            var groups = input.Versions.SelectMany(version => version.ReplacementGroups.Select(x =>
                DietReplacementGroup.Create(x.Id, version.Id, x.TargetMealId, x.TargetMealFoodItemId, x.Title, x.Order))).ToArray();
            var options = input.Versions.SelectMany(v => v.ReplacementGroups.SelectMany(group => group.Options.Select(x =>
                DietReplacementOption.Create(x.Id, group.Id, x.ReplacementFoodItemId,
                    x.ReplacementMealId, x.Quantity, x.Order, x.Notes)))).ToArray();
            return new(plan, notes, versions, meals, foods, groups, options);
        }
        catch (ArgumentException exception)
        {
            throw Validation("dietPlan", exception.Message);
        }
    }

    private static Dictionary<string, string[]> ValidateShape(DietPlanInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Versions.Count == 0) errors["versions"] = ["At least one plan version is required."];
        var allIds = input.Notes.Select(x => x.Id)
            .Concat(input.Versions.Select(x => x.Id))
            .Concat(input.Versions.SelectMany(x => x.Meals).Select(x => x.Id))
            .Concat(input.Versions.SelectMany(x => x.Meals).SelectMany(x => x.FoodItems).Select(x => x.Id))
            .Concat(input.Versions.SelectMany(x => x.ReplacementGroups).Select(x => x.Id))
            .Concat(input.Versions.SelectMany(x => x.ReplacementGroups).SelectMany(x => x.Options).Select(x => x.Id)).ToArray();
        if (allIds.Any(x => x == Guid.Empty) || allIds.Distinct().Count() != allIds.Length)
            errors["ids"] = ["Every nested identifier must be non-empty and unique."];
        CheckOrders(input.Notes.Select(x => x.Order), "notes.order", errors);
        CheckOrders(input.Versions.Select(x => x.Order), "versions.order", errors);
        foreach (var version in input.Versions)
        {
            if (version.Meals.Count == 0) errors[$"versions.{version.Id}.meals"] = ["At least one meal is required."];
            CheckOrders(version.Meals.Select(x => x.Order), $"versions.{version.Id}.meals.order", errors);
            CheckOrders(version.ReplacementGroups.Select(x => x.Order), $"versions.{version.Id}.replacementGroups.order", errors);
            var mealIds = version.Meals.Select(x => x.Id).ToHashSet();
            var itemMeals = version.Meals.SelectMany(meal => meal.FoodItems.Select(item => new { item.Id, MealId = meal.Id }))
                .ToDictionary(x => x.Id, x => x.MealId);
            foreach (var meal in version.Meals)
                CheckOrders(meal.FoodItems.Select(x => x.Order), $"meals.{meal.Id}.foodItems.order", errors);
            foreach (var group in version.ReplacementGroups)
            {
                if (!mealIds.Contains(group.TargetMealId)) errors[$"replacementGroups.{group.Id}.targetMealId"] = ["Target meal must belong to this version."];
                if (group.TargetMealFoodItemId.HasValue &&
                    (!itemMeals.TryGetValue(group.TargetMealFoodItemId.Value, out var ownerMealId) || ownerMealId != group.TargetMealId))
                    errors[$"replacementGroups.{group.Id}.targetMealFoodItemId"] = ["Target food row must belong to the target meal."];
                if (group.Options.Count == 0)
                    errors[$"replacementGroups.{group.Id}.options"] = ["At least one replacement option is required."];
                if (group.Options.Any(x => x.ReplacementMealId.HasValue && !mealIds.Contains(x.ReplacementMealId.Value)))
                    errors[$"replacementGroups.{group.Id}.replacementMealId"] = ["Replacement meal must belong to this version."];
                CheckOrders(group.Options.Select(x => x.Order), $"replacementGroups.{group.Id}.options.order", errors);
            }
        }
        return errors;
    }

    private static void CheckOrders(IEnumerable<int> orders, string key, IDictionary<string, string[]> errors)
    {
        var values = orders.ToArray();
        if (values.Any(x => x < 0) || values.Distinct().Count() != values.Length)
            errors[key] = ["Orders must be non-negative and unique within their parent."];
    }

    private async Task ValidateReferencesAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        var ids = aggregate.FoodItems.Select(x => x.FoodItemId)
            .Concat(aggregate.ReplacementOptions.Where(x => x.ReplacementFoodItemId.HasValue)
                .Select(x => x.ReplacementFoodItemId!.Value)).Distinct().ToArray();
        var foods = await repository.FindFoodsAsync(ids, cancellationToken);
        var missing = ids.Where(x => !foods.ContainsKey(x)).ToArray();
        if (missing.Length > 0) throw Validation("foodItemIds", "One or more food items do not exist.");
    }

    private async Task ValidateClientAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (clientId.HasValue && !await repository.ClientExistsAsync(clientId.Value, cancellationToken))
            throw Validation("clientId", "The selected client does not exist.");
    }

    private async Task<DietPlanResponse> MapAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        var foodIds = aggregate.FoodItems.Select(x => x.FoodItemId)
            .Concat(aggregate.ReplacementOptions.Where(x => x.ReplacementFoodItemId.HasValue).Select(x => x.ReplacementFoodItemId!.Value))
            .Distinct().ToArray();
        var foods = await repository.FindFoodsAsync(foodIds, cancellationToken);
        var mealTotals = aggregate.Meals.ToDictionary(
            meal => meal.Id,
            meal => NutritionCalculator.Round(aggregate.FoodItems.Where(x => x.MealId == meal.Id)
                .Aggregate(NutritionTotals.Zero, (sum, row) => sum + NutritionCalculator.Calculate(foods[row.FoodItemId], row.Quantity))));
        var versions = aggregate.Versions.OrderBy(x => x.Order).Select(version =>
        {
            var meals = aggregate.Meals.Where(x => x.DietPlanVersionId == version.Id).OrderBy(x => x.Order).Select(meal =>
                new MealResponse(meal.Id, meal.NameEn, meal.NameAr, meal.Order, meal.Notes,
                    aggregate.FoodItems.Where(x => x.MealId == meal.Id).OrderBy(x => x.Order).Select(row =>
                    {
                        var food = foods[row.FoodItemId];
                        return new MealFoodItemResponse(row.Id, row.FoodItemId, food.NameEn, food.NameAr,
                            food.MeasurementUnit, row.Quantity, row.Order, row.Notes,
                            NutritionCalculator.Calculate(food, row.Quantity));
                    }).ToArray(), mealTotals[meal.Id])).ToArray();
            var groups = aggregate.ReplacementGroups.Where(x => x.DietPlanVersionId == version.Id).OrderBy(x => x.Order).Select(group =>
                new DietReplacementGroupResponse(group.Id, group.TargetMealId, group.TargetMealFoodItemId,
                    group.Title, group.Order,
                    aggregate.ReplacementOptions.Where(x => x.DietReplacementGroupId == group.Id).OrderBy(x => x.Order).Select(option =>
                        new DietReplacementOptionResponse(option.Id, option.ReplacementFoodItemId,
                            option.ReplacementMealId,
                            option.ReplacementFoodItemId.HasValue
                                ? foods[option.ReplacementFoodItemId.Value].NameEn
                                : aggregate.Meals.Single(x => x.Id == option.ReplacementMealId!.Value).NameEn,
                            option.ReplacementFoodItemId.HasValue
                                ? foods[option.ReplacementFoodItemId.Value].NameAr
                                : aggregate.Meals.Single(x => x.Id == option.ReplacementMealId!.Value).NameAr,
                            option.Quantity, option.Order, option.Notes,
                            option.ReplacementFoodItemId.HasValue
                                ? NutritionCalculator.Calculate(foods[option.ReplacementFoodItemId.Value], option.Quantity!.Value)
                                : mealTotals[option.ReplacementMealId!.Value])).ToArray())).ToArray();
            var total = NutritionCalculator.Round(meals.Aggregate(NutritionTotals.Zero, (sum, meal) => sum + meal.Totals));
            return new DietPlanVersionResponse(version.Id, version.NameEn, version.NameAr, version.Order,
                version.IsActiveForPdf, version.Notes, meals, groups, total);
        }).ToArray();
        return new DietPlanResponse(aggregate.Plan.Id, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
            aggregate.Plan.ClientId, aggregate.Plan.CreatedAt,
            aggregate.Notes.OrderBy(x => x.Order).Select(x => new DietPlanNoteResponse(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
            versions, NutritionCalculator.Round(versions.Aggregate(NutritionTotals.Zero, (sum, version) => sum + version.Totals)));
    }

    private async Task<DietPlanAggregate> RequiredAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, tracking, cancellationToken) ?? throw new NotFoundException("Diet plan", id);
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
    private static void TryDomain(Action action)
    {
        try { action(); }
        catch (ArgumentException exception) { throw Validation("dietPlan", exception.Message); }
    }
}
