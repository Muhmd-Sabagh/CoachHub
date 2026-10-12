using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;

namespace CoachHub.Application.DietPlanning;

/// <summary>
/// Diet plans with version history (D4, §59). Every save is explicit: "save in place" rewrites the
/// current version; "save as new version" archives it (read-only from then on) and adds the next.
/// </summary>
public sealed class DietPlanService(
    IDietPlanRepository repository, CoachOwnership ownership, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<DietPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await MapAsync(await RequiredAsync(id, null, false, cancellationToken), cancellationToken);

    public async Task<DietPlanResponse> GetVersionAsync(Guid id, int versionNumber, CancellationToken cancellationToken) =>
        await MapAsync(await RequiredAsync(id, versionNumber, false, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<PlanRevisionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var revisions = await repository.ListRevisionsAsync(id, cancellationToken) ?? throw new NotFoundException("Diet plan", id);
        var names = await repository.FindAuthorNamesAsync(Authors(revisions), cancellationToken);
        return revisions.OrderByDescending(x => x.VersionNumber).Select(x => PlanRevisions.Info(x, names)).ToArray();
    }

    public async Task<DietPlanResponse> CreateAsync(DietPlanInput input, CancellationToken cancellationToken) =>
        await CreateForCoachAsync(await ResolveCoachAsync(input.ClientId, input.CoachId, cancellationToken), input, cancellationToken);

    private async Task<DietPlanResponse> CreateForCoachAsync(Guid coachId, DietPlanInput input, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        DietPlan? plan = null;
        DietPlanRevision? revision = null;
        TryDomain(() =>
        {
            plan = DietPlan.Create(coachId, input.NameEn, input.NameAr, input.ClientId, now);
            revision = DietPlanRevision.Create(plan.Id, 1, input.Status, input.StartDate, input.EndDate, currentUser.UserId, now);
            revision.SetTargets(input.Targets ?? DietTargets.None);
        });
        var aggregate = Build(plan!, revision!, input);
        await ValidateReferencesAsync(aggregate, new HashSet<Guid>(), cancellationToken);
        await repository.AddAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Save in place: the current version takes the new content, dates and status.</summary>
    public async Task<DietPlanResponse> SaveInPlaceAsync(
        Guid id, DietPlanInput input, CancellationToken cancellationToken)
    {
        var existing = await RequiredAsync(id, null, true, cancellationToken);
        PlanRevisions.EnsureBase(existing.Revision, input.BaseVersionNumber);
        await ValidateClientAsync(existing.Plan.CoachId, input.ClientId, cancellationToken);
        TryDomain(() =>
        {
            existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId);
            existing.Revision.Update(input.Status, input.StartDate, input.EndDate, timeProvider.GetUtcNow());
            existing.Revision.SetTargets(input.Targets ?? DietTargets.None);
        });
        var aggregate = Build(existing.Plan, existing.Revision, input);
        await ValidateReferencesAsync(aggregate, FoodIds(existing), cancellationToken);
        await repository.ReplaceContentAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>
    /// Save as new version: the content becomes version N+1 (with fresh identifiers) and version N is
    /// archived unchanged.
    /// </summary>
    public async Task<DietPlanResponse> SaveAsNewVersionAsync(
        Guid id, DietPlanInput input, CancellationToken cancellationToken)
    {
        var existing = await RequiredAsync(id, null, true, cancellationToken);
        PlanRevisions.EnsureBase(existing.Revision, input.BaseVersionNumber);
        await ValidateClientAsync(existing.Plan.CoachId, input.ClientId, cancellationToken);
        ThrowIfInvalid(input);
        var now = timeProvider.GetUtcNow();
        DietPlanRevision? next = null;
        TryDomain(() =>
        {
            existing.Plan.Update(input.NameEn, input.NameAr, input.ClientId);
            next = DietPlanRevision.Create(existing.Plan.Id, existing.Revision.VersionNumber + 1,
                input.Status, input.StartDate, input.EndDate, currentUser.UserId, now);
            next.SetTargets(input.Targets ?? DietTargets.None);
        });
        var aggregate = Build(existing.Plan, next!, WithNewIdentifiers(input));
        await ValidateReferencesAsync(aggregate, FoodIds(existing), cancellationToken);
        existing.Revision.Archive(now);
        await repository.AddRevisionAsync(aggregate, cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Duplicates the current version into a new plan (§12), which starts at version 1.</summary>
    public async Task<DietPlanResponse> CopyAsync(
        Guid id, CopyDietPlanInput input, CancellationToken cancellationToken)
    {
        var source = await RequiredAsync(id, null, false, cancellationToken);
        await ValidateClientAsync(source.Plan.CoachId, input.ClientId, cancellationToken);
        var content = ToInput(source) with { NameEn = input.NameEn, NameAr = input.NameAr, ClientId = input.ClientId };
        return await CreateForCoachAsync(source.Plan.CoachId, WithNewIdentifiers(content), cancellationToken);
    }

    public async Task<DietPlanResponse> AssignAsync(
        Guid id, Guid? clientId, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, null, true, cancellationToken);
        await ValidateClientAsync(aggregate.Plan.CoachId, clientId, cancellationToken);
        TryDomain(() => aggregate.Plan.Assign(clientId));
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    /// <summary>Shows or hides a note of the current version in the PDF (a save in place).</summary>
    public async Task<DietPlanResponse> SetNoteActiveAsync(
        Guid id, Guid noteId, bool isActive, CancellationToken cancellationToken)
    {
        var aggregate = await RequiredAsync(id, null, true, cancellationToken);
        var note = aggregate.Notes.SingleOrDefault(x => x.Id == noteId)
            ?? throw new NotFoundException("Diet plan note", noteId);
        note.SetActive(isActive);
        aggregate.Revision.Touch(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(aggregate, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await RequiredAsync(id, null, false, cancellationToken);
        await repository.DeleteAsync(id, cancellationToken);
    }

    private static DietPlanAggregate Build(DietPlan plan, DietPlanRevision revision, DietPlanInput input)
    {
        ThrowIfInvalid(input);
        var revisionId = revision.Id;
        try
        {
            var notes = input.Notes.Select(x => DietPlanNote.Create(x.Id, revisionId, x.Text, x.Order, x.IsActive)).ToArray();
            var variants = input.Variants.Select(x => DietPlanVariant.Create(
                x.Id, revisionId, x.NameEn, x.NameAr, x.Order, x.IsActiveForPdf, x.Notes)).ToArray();
            var meals = input.Variants.SelectMany(variant => variant.Meals.Select(x => Meal.Create(
                x.Id, revisionId, variant.Id, x.NameEn, x.NameAr, x.Order, x.Notes, x.Type))).ToArray();
            var foods = input.Variants.SelectMany(v => v.Meals.SelectMany(meal => meal.FoodItems.Select(x =>
                MealFoodItem.Create(x.Id, revisionId, meal.Id, x.FoodItemId, x.Quantity, x.Order, x.Notes, x.FoodUnitId)))).ToArray();
            var groups = input.Variants.SelectMany(variant => variant.ReplacementGroups.Select(x =>
                DietReplacementGroup.Create(x.Id, revisionId, variant.Id, x.TargetMealId, x.TargetMealFoodItemId, x.Title, x.Order))).ToArray();
            var options = input.Variants.SelectMany(v => v.ReplacementGroups.SelectMany(group => group.Options.Select(x =>
                DietReplacementOption.Create(x.Id, revisionId, group.Id, x.ReplacementFoodItemId,
                    x.ReplacementMealId, x.Quantity, x.Order, x.Notes)))).ToArray();
            return new(plan, revision, revision.VersionNumber, notes, variants, meals, foods, groups, options);
        }
        catch (ArgumentException exception)
        {
            throw Validation("dietPlan", exception.Message);
        }
    }

    /// <summary>The version's content as editor input, keeping its identifiers.</summary>
    private static DietPlanInput ToInput(DietPlanAggregate source) => new(
        source.Plan.NameEn, source.Plan.NameAr, source.Plan.ClientId,
        source.Notes.Select(x => new DietPlanNoteInput(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
        source.Variants.Select(variant => new DietPlanVariantInput(
            variant.Id, variant.NameEn, variant.NameAr, variant.Order, variant.IsActiveForPdf, variant.Notes,
            source.Meals.Where(x => x.DietPlanVariantId == variant.Id).Select(meal => new MealInput(
                meal.Id, meal.NameEn, meal.NameAr, meal.Order, meal.Notes,
                source.FoodItems.Where(x => x.MealId == meal.Id).Select(item =>
                    new MealFoodItemInput(item.Id, item.FoodItemId, item.Quantity, item.Order, item.Notes, item.FoodUnitId)).ToArray(),
                meal.Type)).ToArray(),
            source.ReplacementGroups.Where(x => x.DietPlanVariantId == variant.Id).Select(group =>
                new DietReplacementGroupInput(group.Id, group.TargetMealId, group.TargetMealFoodItemId, group.Title, group.Order,
                    source.ReplacementOptions.Where(x => x.DietReplacementGroupId == group.Id).Select(option =>
                        new DietReplacementOptionInput(option.Id, option.ReplacementFoodItemId, option.ReplacementMealId,
                            option.Quantity, option.Order, option.Notes)).ToArray())).ToArray())).ToArray(),
        null, source.Revision.StartDate, source.Revision.EndDate,
        source.Revision.IsArchived ? PlanRevisionStatus.Active : source.Revision.Status,
        Targets: source.Revision.Targets);

    /// <summary>
    /// The same content under fresh identifiers, with internal references (alternative targets,
    /// whole-meal alternatives) remapped. Used for copies and new versions, whose rows never share keys
    /// with the rows they came from.
    /// </summary>
    private static DietPlanInput WithNewIdentifiers(DietPlanInput input)
    {
        var meals = input.Variants.SelectMany(x => x.Meals).ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var items = input.Variants.SelectMany(x => x.Meals).SelectMany(x => x.FoodItems).ToDictionary(x => x.Id, _ => Guid.NewGuid());
        return input with
        {
            Notes = input.Notes.Select(x => x with { Id = Guid.NewGuid() }).ToArray(),
            Variants = input.Variants.Select(variant => variant with
            {
                Id = Guid.NewGuid(),
                Meals = variant.Meals.Select(meal => meal with
                {
                    Id = meals[meal.Id],
                    FoodItems = meal.FoodItems.Select(item => item with { Id = items[item.Id] }).ToArray()
                }).ToArray(),
                ReplacementGroups = variant.ReplacementGroups.Select(group => group with
                {
                    Id = Guid.NewGuid(),
                    TargetMealId = meals[group.TargetMealId],
                    TargetMealFoodItemId = group.TargetMealFoodItemId is { } item ? items[item] : null,
                    Options = group.Options.Select(option => option with
                    {
                        Id = Guid.NewGuid(),
                        ReplacementMealId = option.ReplacementMealId is { } meal ? meals[meal] : null
                    }).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }

    private static void ThrowIfInvalid(DietPlanInput input)
    {
        var errors = ValidateShape(input);
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static Dictionary<string, string[]> ValidateShape(DietPlanInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Variants.Count == 0) errors["variants"] = ["At least one plan variant is required."];
        var allIds = input.Notes.Select(x => x.Id)
            .Concat(input.Variants.Select(x => x.Id))
            .Concat(input.Variants.SelectMany(x => x.Meals).Select(x => x.Id))
            .Concat(input.Variants.SelectMany(x => x.Meals).SelectMany(x => x.FoodItems).Select(x => x.Id))
            .Concat(input.Variants.SelectMany(x => x.ReplacementGroups).Select(x => x.Id))
            .Concat(input.Variants.SelectMany(x => x.ReplacementGroups).SelectMany(x => x.Options).Select(x => x.Id)).ToArray();
        if (allIds.Any(x => x == Guid.Empty) || allIds.Distinct().Count() != allIds.Length)
            errors["ids"] = ["Every nested identifier must be non-empty and unique."];
        CheckOrders(input.Notes.Select(x => x.Order), "notes.order", errors);
        CheckOrders(input.Variants.Select(x => x.Order), "variants.order", errors);
        foreach (var variant in input.Variants)
        {
            if (variant.Meals.Count == 0) errors[$"variants.{variant.Id}.meals"] = ["At least one meal is required."];
            CheckOrders(variant.Meals.Select(x => x.Order), $"variants.{variant.Id}.meals.order", errors);
            CheckOrders(variant.ReplacementGroups.Select(x => x.Order), $"variants.{variant.Id}.replacementGroups.order", errors);
            var mealIds = variant.Meals.Select(x => x.Id).ToHashSet();
            var itemMeals = variant.Meals.SelectMany(meal => meal.FoodItems.Select(item => new { item.Id, MealId = meal.Id }))
                .DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.MealId);
            foreach (var meal in variant.Meals)
                CheckOrders(meal.FoodItems.Select(x => x.Order), $"meals.{meal.Id}.foodItems.order", errors);
            foreach (var group in variant.ReplacementGroups)
            {
                if (!mealIds.Contains(group.TargetMealId)) errors[$"replacementGroups.{group.Id}.targetMealId"] = ["Target meal must belong to this variant."];
                if (group.TargetMealFoodItemId.HasValue &&
                    (!itemMeals.TryGetValue(group.TargetMealFoodItemId.Value, out var ownerMealId) || ownerMealId != group.TargetMealId))
                    errors[$"replacementGroups.{group.Id}.targetMealFoodItemId"] = ["Target food row must belong to the target meal."];
                if (group.Options.Count == 0)
                    errors[$"replacementGroups.{group.Id}.options"] = ["At least one replacement option is required."];
                if (group.Options.Any(x => x.ReplacementMealId.HasValue && !mealIds.Contains(x.ReplacementMealId.Value)))
                    errors[$"replacementGroups.{group.Id}.replacementMealId"] = ["Replacement meal must belong to this variant."];
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

    /// <summary>
    /// Every food must be one the caller can see (platform or the coach's own), except foods the version
    /// already used: a transferred client's plan may keep the previous coach's custom foods (D7).
    /// Units must belong to their food.
    /// </summary>
    private async Task ValidateReferencesAsync(
        DietPlanAggregate aggregate, IReadOnlySet<Guid> alreadyUsed, CancellationToken cancellationToken)
    {
        var ids = FoodIds(aggregate).ToArray();
        var visible = await repository.FindFoodsAsync(ids, cancellationToken);
        var kept = ids.Where(id => !visible.ContainsKey(id) && alreadyUsed.Contains(id)).ToArray();
        var foods = kept.Length == 0
            ? visible
            : visible.Concat(await repository.FindReferencedFoodsAsync(kept, cancellationToken))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        if (ids.Any(id => !foods.ContainsKey(id))) throw Validation("foodItemIds", "One or more food items do not exist.");
        if (aggregate.FoodItems.Any(row => row.FoodUnitId is { } unit && foods[row.FoodItemId].FindUnit(unit) is null))
            throw Validation("foodUnitId", "A unit does not belong to its food.");
    }

    private static HashSet<Guid> FoodIds(DietPlanAggregate aggregate) =>
        aggregate.FoodItems.Select(x => x.FoodItemId)
            .Concat(aggregate.ReplacementOptions.Where(x => x.ReplacementFoodItemId.HasValue)
                .Select(x => x.ReplacementFoodItemId!.Value)).ToHashSet();

    /// <summary>A client-assigned plan belongs to the client's coach; a template to the creating coach.</summary>
    private async Task<Guid> ResolveCoachAsync(Guid? clientId, Guid? requestedCoachId, CancellationToken cancellationToken)
    {
        if (clientId is { } id)
        {
            var clientCoachId = await repository.FindClientCoachIdAsync(id, cancellationToken)
                ?? throw Validation("clientId", "The selected client does not exist.");
            if (requestedCoachId.HasValue && requestedCoachId.Value != clientCoachId)
                throw Validation("coachId", "The client belongs to another coach.");
            return clientCoachId;
        }
        return await ownership.ForNewRecordAsync(requestedCoachId, cancellationToken);
    }

    private async Task ValidateClientAsync(Guid planCoachId, Guid? clientId, CancellationToken cancellationToken)
    {
        if (clientId is not { } id) return;
        var clientCoachId = await repository.FindClientCoachIdAsync(id, cancellationToken)
            ?? throw Validation("clientId", "The selected client does not exist.");
        if (clientCoachId != planCoachId)
            throw Validation("clientId", "The client belongs to another coach.");
    }

    private async Task<DietPlanResponse> MapAsync(DietPlanAggregate aggregate, CancellationToken cancellationToken)
    {
        var foodIds = aggregate.FoodItems.Select(x => x.FoodItemId)
            .Concat(aggregate.ReplacementOptions.Where(x => x.ReplacementFoodItemId.HasValue).Select(x => x.ReplacementFoodItemId!.Value))
            .Distinct().ToArray();
        // The plan is visible, so the foods it references are readable as part of it.
        var foods = await repository.FindReferencedFoodsAsync(foodIds, cancellationToken);
        var authors = await repository.FindAuthorNamesAsync(Authors([aggregate.Revision]), cancellationToken);
        var mealTotals = aggregate.Meals.ToDictionary(
            meal => meal.Id,
            meal => NutritionCalculator.Round(aggregate.FoodItems.Where(x => x.MealId == meal.Id)
                .Aggregate(NutritionTotals.Zero, (sum, row) => sum + NutritionCalculator.Calculate(foods[row.FoodItemId], row.Quantity, row.FoodUnitId))));
        var variants = aggregate.Variants.OrderBy(x => x.Order).Select(variant =>
        {
            var meals = aggregate.Meals.Where(x => x.DietPlanVariantId == variant.Id).OrderBy(x => x.Order).Select(meal =>
                new MealResponse(meal.Id, meal.NameEn, meal.NameAr, meal.Type, meal.Order, meal.Notes,
                    aggregate.FoodItems.Where(x => x.MealId == meal.Id).OrderBy(x => x.Order).Select(row =>
                    {
                        var food = foods[row.FoodItemId];
                        var unit = food.FindUnit(row.FoodUnitId);
                        return new MealFoodItemResponse(row.Id, row.FoodItemId, food.NameEn, food.NameAr,
                            row.FoodUnitId, unit?.NameEn ?? food.BaseUnitLabel,
                            unit is null ? food.BaseUnitLabelAr : unit.NameAr ?? unit.NameEn,
                            row.Quantity, row.Order, row.Notes,
                            NutritionCalculator.Calculate(food, row.Quantity, row.FoodUnitId));
                    }).ToArray(), mealTotals[meal.Id])).ToArray();
            var groups = aggregate.ReplacementGroups.Where(x => x.DietPlanVariantId == variant.Id).OrderBy(x => x.Order).Select(group =>
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
            return new DietPlanVariantResponse(variant.Id, variant.NameEn, variant.NameAr, variant.Order,
                variant.IsActiveForPdf, variant.Notes, meals, groups, total);
        }).ToArray();
        return new DietPlanResponse(aggregate.Plan.Id, aggregate.Plan.CoachId, aggregate.Plan.NameEn, aggregate.Plan.NameAr,
            aggregate.Plan.ClientId, aggregate.Plan.CreatedAt,
            PlanRevisions.Info(aggregate.Revision, authors), aggregate.CurrentVersionNumber, aggregate.Revision.Targets,
            aggregate.Notes.OrderBy(x => x.Order).Select(x => new DietPlanNoteResponse(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
            variants, NutritionCalculator.Round(variants.Aggregate(NutritionTotals.Zero, (sum, variant) => sum + variant.Totals)));
    }

    private static Guid[] Authors(IEnumerable<PlanRevision> revisions) =>
        revisions.Where(x => x.CreatedByUserId.HasValue).Select(x => x.CreatedByUserId!.Value).Distinct().ToArray();

    private async Task<DietPlanAggregate> RequiredAsync(
        Guid id, int? versionNumber, bool tracking, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, versionNumber, tracking, cancellationToken)
            ?? (versionNumber is { } number
                ? throw new NotFoundException("Diet plan version", $"{id} v{number}")
                : throw new NotFoundException("Diet plan", id));
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
    private static void TryDomain(Action action)
    {
        try { action(); }
        catch (ArgumentException exception) { throw Validation("dietPlan", exception.Message); }
    }
}
