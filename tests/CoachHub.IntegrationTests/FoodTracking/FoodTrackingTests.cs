using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.Settings;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.FoodTracking;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.FoodTracking;

/// <summary>
/// P8 on SQL Server: flexible meals and alternatives in the plan (§7, §13), the client's food diary (§8),
/// planned versus actual (§9, §10) and coach analytics (§11), with tenant isolation (§62).
/// </summary>
public sealed class FoodTrackingTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Plans_have_meal_options_and_exact_or_flexible_alternatives()
    {
        var setup = await SetUpAsync();
        var plan = setup.Plan;
        var breakfast = plan.Variants[0].Meals[0];
        var lunch = plan.Variants[0].Meals[1];

        // The meal counts its first option; each option has its own totals (§13).
        Assert.Equal(2, breakfast.Options.Count);
        Assert.Equal((214.5m, 120m), (breakfast.Options[0].Totals.Calories, breakfast.Options[1].Totals.Calories));
        Assert.Equal(214.5m, breakfast.Totals.Calories);
        Assert.Equal(462m, plan.Totals.Calories);
        Assert.All(breakfast.FoodItems, row => Assert.Contains(breakfast.Options, option => option.Id == row.MealOptionId));

        // A flexible alternative is calculated by the coach's rule (dominant macro: 46.5 g protein); a set quantity wins.
        var group = Assert.Single(plan.Variants[0].ReplacementGroups);
        Assert.Equal((ReplacementMode.Flexible, lunch.FoodItems[0].Id), (group.Mode, group.TargetMealFoodItemId!.Value));
        Assert.Equal((155m, true, "g", 209.25m), (group.Options[0].EffectiveQuantity!.Value, group.Options[0].IsCalculated,
            group.Options[0].UnitLabel, group.Options[0].Totals.Calories));
        Assert.Equal((120m, false), (group.Options[1].EffectiveQuantity!.Value, group.Options[1].IsCalculated));

        // Lists count the first option too.
        var listed = (await setup.Coach.GetFromJsonAsync<PagedResult<SavedPlanSummary>>(
            $"/api/v1/saved-plans?planName={Uri.EscapeDataString(plan.NameEn)}"))!;
        Assert.Equal(462m, Assert.Single(listed.Items).TotalCalories);

        // Exact alternatives need a quantity; rows of a meal with options name one of them.
        var input = setup.Input;
        var exactWithout = input with { Variants = [input.Variants.First() with { ReplacementGroups = [
            input.Variants.First().ReplacementGroups.First() with { Mode = ReplacementMode.Exact }] }] };
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PostAsJsonAsync("/api/v1/diet-plans", Fresh(exactWithout))).StatusCode);
        var meals = input.Variants.First().Meals.ToArray();
        var orphan = input with { Variants = [input.Variants.First() with { ReplacementGroups = [], Meals = [
            meals[0] with { FoodItems = [meals[0].FoodItems.First() with { MealOptionId = null }, .. meals[0].FoodItems.Skip(1)] }, meals[1]] }] };
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PostAsJsonAsync("/api/v1/diet-plans", Fresh(orphan))).StatusCode);

        // A new version copies the options under new identifiers.
        var current = Edit(plan);
        var next = await ReadAsync<DietPlanResponse>(await setup.Coach.PostAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions", current));
        var nextBreakfast = next.Variants[0].Meals[0];
        Assert.Equal(2, next.Revision.VersionNumber);
        Assert.Equal(2, nextBreakfast.Options.Count);
        Assert.DoesNotContain(nextBreakfast.Options, option => breakfast.Options.Any(old => old.Id == option.Id));
        Assert.All(nextBreakfast.FoodItems, row => Assert.Contains(nextBreakfast.Options, option => option.Id == row.MealOptionId));
        Assert.Equal(155m, next.Variants[0].ReplacementGroups[0].Options[0].EffectiveQuantity);
    }

    [Fact]
    public async Task The_client_logs_their_day_against_the_plan()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        var day = $"/api/v1/me/nutrition/days/{Today:yyyy-MM-dd}";
        var breakfast = setup.Plan.Variants[0].Meals[0];
        var lunch = setup.Plan.Variants[0].Meals[1];
        var chickenRow = lunch.FoodItems[0].Id;

        // The day shows the plan in force with the target (version calories, planned macros) and nothing consumed.
        var start = (await portal.GetFromJsonAsync<NutritionDayResponse>(day))!;
        Assert.Equal((setup.Plan.Id, 2), (start.Plan!.PlanId, start.Plan.Meals.Count));
        Assert.Equal(breakfast.Options[0].Id, start.Plan.Meals[0].ChosenOptionId);
        Assert.StartsWith("Eggs", start.Plan.Meals[0].Foods[0].NameEn);
        Assert.Equal(2, start.Plan.Meals[1].Foods[0].AlternativeCount);
        Assert.Equal((500m, 462m, 0m, 500m), (start.Summary.Target!.Calories, start.Summary.Planned!.Calories,
            start.Summary.Consumed.Calories, start.Summary.Remaining!.Calories));
        Assert.Equal((2, 0, (decimal?)null), (start.Summary.PlannedMeals, start.Summary.CompletedMeals, start.Summary.AdherencePercent));

        // Option B for breakfast (§13), then the whole meal is logged as planned.
        var chosen = await ReadAsync<NutritionDayResponse>(await portal.PutAsJsonAsync(day + "/choices",
            new NutritionDayChoicesInput(null, [new MealOptionChoiceInput(breakfast.Id, breakfast.Options[1].Id)])));
        Assert.StartsWith("Yogurt", chosen.Plan!.Meals[0].Foods.Single().NameEn);
        Assert.Equal(367.5m, chosen.Summary.Planned!.Calories);
        var logged = await ReadAsync<NutritionDayResponse>(await portal.PostAsync($"{day}/meals/{breakfast.Id}/log", null));
        var yogurt = Assert.Single(logged.Entries);
        Assert.Equal((FoodLogSource.Planned, MealType.Breakfast, 200m, 120m), (yogurt.Source, yogurt.Slot, yogurt.Quantity!.Value, yogurt.Nutrients.Calories));
        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsync($"{day}/meals/{breakfast.Id}/log", null)).StatusCode);

        // "Change Food" offers the coach's alternatives only, with quantities (§7); the client eats the turkey instead.
        var alternatives = (await portal.GetFromJsonAsync<FoodAlternativeResponse[]>($"{day}/foods/{chickenRow}/alternatives"))!;
        Assert.Equal(["Turkey", "Tuna"], alternatives.Select(x => x.NameEn.Split(' ')[0]));
        Assert.Equal((155m, true), (alternatives[0].Quantity!.Value, alternatives[0].IsCalculated));
        Assert.Empty((await portal.GetFromJsonAsync<FoodAlternativeResponse[]>($"{day}/foods/{breakfast.FoodItems[0].Id}/alternatives"))!);
        var swap = await ReadAsync<FoodLogEntryResponse>(await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Lunch, PlannedMealFoodItemId: chickenRow, ReplacementOptionId: alternatives[0].OptionId)));
        Assert.Equal((FoodLogSource.Swap, alternatives[0].NameEn, 155m, 209.25m), (swap.Source, swap.NameEn, swap.Quantity!.Value, swap.Nutrients.Calories));
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Lunch, PlannedMealFoodItemId: chickenRow, ReplacementOptionId: Guid.NewGuid()))).StatusCode);

        // Extras: a catalog food in one of its units, and a custom food (§8).
        var bread = await ReadAsync<FoodLogEntryResponse>(await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Snack, setup.Bread.Id, 2, setup.Bread.Units[0].Id)));
        Assert.Equal((FoodLogSource.Extra, "slice", 159m), (bread.Source, bread.UnitLabel, bread.Nutrients.Calories));
        var bar = await ReadAsync<FoodLogEntryResponse>(await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Snack, Custom: new CustomFoodInput("Protein bar", 200, 20, 20, 7))));
        Assert.Equal((true, 200m), (bar.IsCustom, bar.Nutrients.Calories));
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Snack, setup.Bread.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today.AddDays(2), MealType.Snack, setup.Bread.Id, 1))).StatusCode);

        // Edit a quantity, move a slot, delete an entry.
        var halved = await ReadAsync<FoodLogEntryResponse>(await portal.PutAsJsonAsync($"/api/v1/me/nutrition/entries/{bread.Id}",
            new FoodLogEntryUpdate(MealType.Breakfast, 1, setup.Bread.Units[0].Id)));
        Assert.Equal((79.5m, MealType.Breakfast), (halved.Nutrients.Calories, halved.Slot));
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"/api/v1/me/nutrition/entries/{bar.Id}",
            new FoodLogEntryUpdate(Quantity: 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"/api/v1/me/nutrition/entries/{bar.Id}")).StatusCode);

        // Planned versus actual: every planned meal is logged, so the day is complete (§9, §10).
        var end = (await portal.GetFromJsonAsync<NutritionDayResponse>(day))!;
        Assert.Equal(3, end.Entries.Count);
        Assert.Equal((408.8m, 91.2m), (end.Summary.Consumed.Calories, end.Summary.Remaining!.Calories));
        Assert.Equal((2, 100m, true), (end.Summary.CompletedMeals, end.Summary.MealCompletionPercent!.Value, end.Summary.IsCompleted));
        Assert.InRange(end.Summary.AdherencePercent!.Value, 0m, 100m);

        // The diary food search covers platform foods and the coach's own, not another coach's.
        var foods = (await portal.GetFromJsonAsync<PagedResult<FoodResponse>>($"/api/v1/me/nutrition/foods?searchTerm={setup.Tag}"))!;
        Assert.Contains(foods.Items, x => x.Id == setup.Bread.Id);
        Assert.Contains(foods.Items, x => x.Id == setup.CustomFood.Id);
        Assert.DoesNotContain(foods.Items, x => x.Id == setup.OtherCoachFood.Id);

        // A food in a diary cannot be deleted.
        using var admin = factory.CreateAdminClient();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/nutrition/foods/{setup.Bread.Id}")).StatusCode);
    }

    [Fact]
    public async Task Diaries_are_private_to_the_client_and_their_coach()
    {
        var setup = await SetUpAsync();
        var entry = await ReadAsync<FoodLogEntryResponse>(await setup.Portal.PostAsJsonAsync("/api/v1/me/nutrition/entries",
            new FoodLogEntryInput(Today, MealType.Snack, setup.Bread.Id, 1, setup.Bread.Units[0].Id)));
        var dayPath = $"/nutrition/days/{Today:yyyy-MM-dd}";

        // Another client of the same coach sees only their own (empty) day and cannot touch the entry.
        var sibling = await ReadAsync<ClientResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Sibling " + setup.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientAreaClient(setup.CoachId, sibling.Id);
        var siblingDay = (await siblingPortal.GetFromJsonAsync<NutritionDayResponse>("/api/v1/me" + dayPath))!;
        Assert.Equal(((PlannedDayResponse?)null, 0), (siblingDay.Plan, siblingDay.Entries.Count));
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.PutAsJsonAsync($"/api/v1/me/nutrition/entries/{entry.Id}",
            new FoodLogEntryUpdate(Quantity: 3))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.DeleteAsync($"/api/v1/me/nutrition/entries/{entry.Id}")).StatusCode);

        // The coach reads the day and analytics (§11); another coach finds nothing; roles stay in their lanes.
        var coachView = (await setup.Coach.GetFromJsonAsync<NutritionDayResponse>($"/api/v1/clients/{setup.ClientId}{dayPath}"))!;
        Assert.Equal(entry.Id, Assert.Single(coachView.Entries).Id);
        var analytics = (await setup.Coach.GetFromJsonAsync<NutritionAnalyticsResponse>(
            $"/api/v1/clients/{setup.ClientId}/nutrition/analytics?from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))!;
        Assert.Equal((7, 7, 1, 0), (analytics.Days.Count, analytics.Summary.Days, analytics.Summary.LoggedDays, analytics.Summary.CompletedDays));
        Assert.Equal((14.3m, 79.5m), (analytics.Summary.LoggingAdherencePercent!.Value, analytics.Summary.AverageConsumed!.Calories));
        Assert.Equal(7, analytics.Weeks.Sum(x => x.Days));
        Assert.Equal(7, analytics.Months.Sum(x => x.Days));
        Assert.All(analytics.Days, row => Assert.Equal(500m, row.Target!.Calories));
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.GetAsync(
            $"/api/v1/clients/{setup.ClientId}/nutrition/analytics?from={Today.AddDays(-400):yyyy-MM-dd}&to={Today:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"/api/v1/clients/{setup.ClientId}{dayPath}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync(
            $"/api/v1/clients/{setup.ClientId}/nutrition/analytics?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Portal.GetAsync($"/api/v1/clients/{setup.ClientId}{dayPath}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.GetAsync("/api/v1/me" + dayPath)).StatusCode);

        // The diary follows the client to a new coach (D7).
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{setup.ClientId}/transfer", new ClientTransferInput(setup.OtherCoachId))).EnsureSuccessStatusCode();
        var moved = (await setup.OtherCoach.GetFromJsonAsync<NutritionDayResponse>($"/api/v1/clients/{setup.ClientId}{dayPath}"))!;
        Assert.Equal(entry.Id, Assert.Single(moved.Entries).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync($"/api/v1/clients/{setup.ClientId}{dayPath}")).StatusCode);
    }

    private sealed record Setup(
        Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, HttpClient Portal,
        DietPlanInput Input, DietPlanResponse Plan, FoodResponse Bread, FoodResponse CustomFood, FoodResponse OtherCoachFood, string Tag);

    /// <summary>
    /// A coach (dominant-macro replacement rule) with a client and a plan in force: breakfast with option A (3 eggs) or
    /// B (200 g yogurt), lunch of 150 g chicken with flexible alternatives (turkey, calculated; tuna, 120 g). 500 kcal target.
    /// </summary>
    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = factory.CreateCoachClient(coachId);
        var otherCoachId = await factory.CreateCoachAsync();
        var otherCoach = factory.CreateCoachClient(otherCoachId);
        using var admin = factory.CreateAdminClient();
        var category = (await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Tracking " + tag, null)))).Id;
        (await coach.PutAsJsonAsync("/api/v1/settings/coach", new CoachSettingsInput(
            new ActivityFactors(1.2m, 1.375m, 1.55m, 1.725m, 1.9m), 20m, 10m, 2.0m, 0.8m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5), FoodReplacementRule.DominantMacro))).EnsureSuccessStatusCode();

        async Task<FoodResponse> FoodAsync(HttpClient http, FoodInput input) =>
            await ReadAsync<FoodResponse>(await http.PostAsJsonAsync("/api/v1/nutrition/foods", input));
        FoodInput Grams(string name, decimal kcal, decimal protein, decimal carbs, decimal fat) =>
            new($"{name} {tag}", null, category, FoodBaseUnit.Gram, kcal, protein, carbs, fat, null);
        var eggs = await FoodAsync(admin, new FoodInput($"Eggs {tag}", null, category, FoodBaseUnit.Piece, 7150, 600, 50, 500, null,
            PieceNameEn: "egg"));
        var yogurt = await FoodAsync(admin, Grams("Yogurt", 60, 10, 4, 0.4m));
        var chicken = await FoodAsync(admin, Grams("Chicken", 165, 31, 0, 3.6m));
        var turkey = await FoodAsync(admin, Grams("Turkey", 135, 30, 0, 1));
        var tuna = await FoodAsync(admin, Grams("Tuna", 116, 26, 0, 1));
        var bread = await FoodAsync(admin, Grams("Bread", 265, 9, 49, 3.2m) with { Units = [new(null, "slice", "شريحة", 30)] });
        var custom = await FoodAsync(coach, Grams("Coach shake", 100, 20, 3, 1));
        var otherFood = await FoodAsync(otherCoach, Grams("Other shake", 100, 20, 3, 1));

        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Tracker " + tag, null, null, null)));
        Guid optionA = Guid.NewGuid(), optionB = Guid.NewGuid(), chickenRow = Guid.NewGuid(), lunch = Guid.NewGuid();
        var input = new DietPlanInput($"Tracked plan {tag}", null, client.Id, [],
        [
            new(Guid.NewGuid(), "Day", null, 0, true, null,
            [
                new(Guid.NewGuid(), "Breakfast", null, 0, null,
                [
                    new(Guid.NewGuid(), eggs.Id, 3, 0, null, MealOptionId: optionA),
                    new(Guid.NewGuid(), yogurt.Id, 200, 1, null, MealOptionId: optionB),
                ], MealType.Breakfast, [new(optionA, "Option A", null, 0), new(optionB, "Option B", null, 1)]),
                new(lunch, "Lunch", null, 1, null, [new(chickenRow, chicken.Id, 150, 0, null)], MealType.Lunch),
            ],
            [
                new(Guid.NewGuid(), lunch, chickenRow, "Protein swaps", 0,
                [
                    new(Guid.NewGuid(), turkey.Id, null, null, 0, null),
                    new(Guid.NewGuid(), tuna.Id, null, 120, 1, null),
                ], ReplacementMode.Flexible),
            ]),
        ], Targets: new DietTargets(Calories: 500));
        var plan = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", input));
        return new Setup(coachId, coach, otherCoachId, otherCoach, client.Id, factory.CreateClientAreaClient(coachId, client.Id),
            input, plan, bread, custom, otherFood, tag);
    }

    /// <summary>The plan's current content as editor input, keeping identifiers (for a save based on version 1).</summary>
    private static DietPlanInput Edit(DietPlanResponse plan) => new(
        plan.NameEn, plan.NameAr, plan.ClientId, [],
        plan.Variants.Select(variant => new DietPlanVariantInput(variant.Id, variant.NameEn, variant.NameAr, variant.Order,
            variant.IsActiveForPdf, variant.Notes,
            variant.Meals.Select(meal => new MealInput(meal.Id, meal.NameEn, meal.NameAr, meal.Order, meal.Notes,
                meal.FoodItems.Select(row => new MealFoodItemInput(row.Id, row.FoodItemId, row.Quantity, row.Order, row.Notes,
                    row.FoodUnitId, row.MealOptionId)).ToArray(),
                meal.Type, meal.Options.Select(option => new MealOptionInput(option.Id, option.NameEn, option.NameAr, option.Order)).ToArray())).ToArray(),
            variant.ReplacementGroups.Select(group => new DietReplacementGroupInput(group.Id, group.TargetMealId, group.TargetMealFoodItemId,
                group.Title, group.Order, group.Options.Select(option => new DietReplacementOptionInput(option.Id, option.ReplacementFoodItemId,
                    option.ReplacementMealId, option.Quantity, option.Order, option.Notes, option.FoodUnitId)).ToArray(), group.Mode)).ToArray())).ToArray(),
        BaseVersionNumber: plan.Revision.VersionNumber, Targets: plan.Targets);

    /// <summary>The same content under new identifiers, so it can be created as another plan.</summary>
    private static DietPlanInput Fresh(DietPlanInput input)
    {
        var map = new Dictionary<Guid, Guid>();
        Guid New(Guid id) => map.TryGetValue(id, out var value) ? value : map[id] = Guid.NewGuid();
        Guid? Maybe(Guid? id) => id is { } value ? New(value) : null;
        return input with
        {
            Variants = input.Variants.Select(variant => variant with
            {
                Id = New(variant.Id),
                Meals = variant.Meals.Select(meal => meal with
                {
                    Id = New(meal.Id),
                    Options = meal.Options?.Select(option => option with { Id = New(option.Id) }).ToArray(),
                    FoodItems = meal.FoodItems.Select(row => row with { Id = New(row.Id), MealOptionId = Maybe(row.MealOptionId) }).ToArray()
                }).ToArray(),
                ReplacementGroups = variant.ReplacementGroups.Select(group => group with
                {
                    Id = New(group.Id), TargetMealId = New(group.TargetMealId), TargetMealFoodItemId = Maybe(group.TargetMealFoodItemId),
                    Options = group.Options.Select(option => option with { Id = New(option.Id) }).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
