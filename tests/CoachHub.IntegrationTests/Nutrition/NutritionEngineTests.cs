using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Settings;
using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.Settings;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Nutrition;

/// <summary>
/// P7 on SQL Server: platform and custom foods (§5) with units, the diet builder's units, meal types,
/// fiber and targets (§4, §6), and the calorie and macro engine with coach settings (§14, §15, §49).
/// </summary>
public sealed class NutritionEngineTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Custom_foods_belong_to_their_coach_and_platform_foods_to_administrators()
    {
        var coachAId = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachAId);
        var coachB = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var category = await CategoryAsync(admin);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var platform = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods", Food("Platform rice " + tag, category)));
        var custom = await ReadAsync<FoodResponse>(await coachA.PostAsJsonAsync("/api/v1/nutrition/foods",
            Food("House granola " + tag, category) with { Units = [new(null, "bowl", "وعاء", 60)], FiberPer100 = 8 }));
        Assert.Equal((true, coachAId), (custom.IsCustom, custom.CoachId!.Value));
        Assert.Equal(("bowl", 60m), (custom.Units.Single().NameEn, custom.Units.Single().BaseQuantity));

        // Search covers both catalogs; another coach never sees the custom food.
        var mine = await ListAsync(coachA, $"searchTerm={tag}");
        Assert.Equal(2, mine.TotalCount);
        Assert.Equal(custom.Id, Assert.Single((await ListAsync(coachA, $"searchTerm={tag}&scope=Custom")).Items).Id);
        Assert.Equal(platform.Id, Assert.Single((await ListAsync(coachA, $"searchTerm={tag}&scope=Platform")).Items).Id);
        Assert.Equal(platform.Id, Assert.Single((await ListAsync(coachB, $"searchTerm={tag}")).Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/nutrition/foods/{custom.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.PutAsJsonAsync($"/api/v1/nutrition/foods/{custom.Id}", Food("x", category))).StatusCode);

        // Coaches edit their own foods only; platform foods stay with administrators.
        var unitId = custom.Units.Single().Id;
        var edited = await ReadAsync<FoodResponse>(await coachA.PutAsJsonAsync($"/api/v1/nutrition/foods/{custom.Id}",
            Food("House granola " + tag, category) with { Units = [new(unitId, "bowl", "وعاء", 70)] }));
        Assert.Equal((unitId, 70m), (edited.Units.Single().Id, edited.Units.Single().BaseQuantity));
        Assert.Equal(HttpStatusCode.Forbidden, (await coachA.PutAsJsonAsync($"/api/v1/nutrition/foods/{platform.Id}", Food("Mine now", category))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coachA.DeleteAsync($"/api/v1/nutrition/foods/{platform.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coachA.PostAsJsonAsync("/api/v1/nutrition/foods",
            Food("Other coach", category) with { CoachId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coachA.PostAsJsonAsync("/api/v1/nutrition/foods",
            Food("No piece name", category) with { BaseUnit = FoodBaseUnit.Piece })).StatusCode);

        // An administrator can create a custom food for a named coach.
        var forA = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            Food("Coach A shake " + tag, category) with { CoachId = coachAId }));
        Assert.Equal(coachAId, forA.CoachId);
        Assert.Equal(3, (await ListAsync(coachA, $"searchTerm={tag}")).TotalCount);

        // Another coach's food cannot go into a plan.
        Assert.Equal(HttpStatusCode.BadRequest, (await coachB.PostAsJsonAsync("/api/v1/diet-plans", Diet(custom.Id, 50))).StatusCode);

        // A food used by a plan cannot be deleted, nor can a unit a plan uses be removed.
        var plan = await ReadAsync<DietPlanResponse>(await coachA.PostAsJsonAsync("/api/v1/diet-plans", Diet(custom.Id, 2, unitId)));
        Assert.Equal(HttpStatusCode.Conflict, (await coachA.DeleteAsync($"/api/v1/nutrition/foods/{custom.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await coachA.PutAsJsonAsync($"/api/v1/nutrition/foods/{custom.Id}",
            Food("House granola " + tag, category))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coachA.DeleteAsync($"/api/v1/diet-plans/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coachA.DeleteAsync($"/api/v1/nutrition/foods/{custom.Id}")).StatusCode);
    }

    [Fact]
    public async Task Diet_builder_uses_units_meal_types_fiber_and_version_targets()
    {
        var coach = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var category = await CategoryAsync(admin);
        var oats = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Oats " + Guid.NewGuid().ToString("N")[..6], "شوفان", category, FoodBaseUnit.Gram, 389, 16.9m, 66.3m, 6.9m, null,
                FiberPer100: 10.6m, ServingSize: 80, Units: [new(null, "cup", "كوب", 80)])));
        var eggs = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Eggs " + Guid.NewGuid().ToString("N")[..6], null, category, FoodBaseUnit.Piece, 7150, 600, 50, 500, null,
                PieceNameEn: "large egg", PieceNameAr: "بيضة")));
        var cup = oats.Units.Single().Id;
        var targets = new DietTargets(2200, 160, 230, 70, 3, 2);

        var plan = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", new DietPlanInput(
            "Units plan", null, null, [],
            [new(Guid.NewGuid(), "Training day", null, 0, true, null,
                [
                    new(Guid.NewGuid(), "Breakfast", null, 0, null,
                        [new(Guid.NewGuid(), oats.Id, 1.5m, 0, null, cup), new(Guid.NewGuid(), eggs.Id, 3, 1, null)]),
                    new(Guid.NewGuid(), "Shake", null, 1, null, [new(Guid.NewGuid(), oats.Id, 30, 0, null)], MealType.Snack)
                ], [])],
            Targets: targets)));

        var breakfast = plan.Variants[0].Meals[0];
        Assert.Equal(MealType.Breakfast, breakfast.Type);
        Assert.Equal(MealType.Snack, plan.Variants[0].Meals[1].Type);
        Assert.Equal(("cup", "كوب", 120m), (breakfast.FoodItems[0].UnitLabel, breakfast.FoodItems[0].UnitLabelAr, breakfast.FoodItems[0].Totals.Weight));
        Assert.Equal(466.8m, breakfast.FoodItems[0].Totals.Calories);
        Assert.Equal(("large egg", "بيضة", 214.5m), (breakfast.FoodItems[1].UnitLabel, breakfast.FoodItems[1].UnitLabelAr, breakfast.FoodItems[1].Totals.Calories));
        Assert.Equal(12.72m + 3.18m, plan.Totals.Fiber);
        Assert.Equal(466.8m + 214.5m + 116.7m, plan.Totals.Calories);
        Assert.Equal(targets, plan.Targets);

        // The saved-plans list totals apply units too.
        var listed = await coach.GetFromJsonAsync<PagedResult<CoachHub.Application.SavedPlans.SavedPlanSummary>>("/api/v1/saved-plans?planName=Units");
        Assert.Equal(plan.Totals.Calories, Assert.Single(listed!.Items).TotalCalories);

        // Targets belong to the version: a new version carries its own.
        var input = new DietPlanInput("Units plan", null, null, [],
            [new(Guid.NewGuid(), "Training day", null, 0, true, null,
                [new(Guid.NewGuid(), "Breakfast", null, 0, null, [new(Guid.NewGuid(), oats.Id, 1, 0, null, cup)])], [])],
            BaseVersionNumber: 1, Targets: targets with { Calories = 2000 });
        var second = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions", input));
        Assert.Equal(2000m, second.Targets.Calories);
        Assert.Equal(2200m, (await coach.GetFromJsonAsync<DietPlanResponse>($"/api/v1/diet-plans/{plan.Id}/revisions/1"))!.Targets.Calories);

        // A unit of another food, and out-of-range targets, are rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/diet-plans", Diet(eggs.Id, 1, cup))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/diet-plans",
            Diet(eggs.Id, 1) with { Targets = new DietTargets(Calories: 50000) })).StatusCode);
    }

    [Fact]
    public async Task Client_targets_come_from_the_profile_macro_mode_and_coach_settings()
    {
        var coach = await factory.CreateCoachClientAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Target client", null, null, null)));

        var missing = await coach.GetAsync($"/api/v1/clients/{client.Id}/nutrition-targets");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var problem = await missing.Content.ReadAsStringAsync();
        Assert.Contains("dateOfBirth", problem);
        Assert.Contains("activityLevel", problem);

        var birth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30).AddDays(-1);
        (await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile", Profile(birth, null))).EnsureSuccessStatusCode();
        var defaults = await coach.GetFromJsonAsync<EnergyCalculatorResponse>($"/api/v1/clients/{client.Id}/nutrition-targets");
        Assert.Equal((30, 2207.2m, MacroMode.GramsPerKg, 160m), (defaults!.Age, defaults.GoalCalories, defaults.Macros.Mode, defaults.Protein));

        // The coach's settings change the result: a 25% deficit and 1.6 for moderate activity.
        (await coach.PutAsJsonAsync("/api/v1/settings/coach", new CoachSettingsInput(
            new ActivityFactors(1.2m, 1.4m, 1.6m, 1.8m, 2.0m), 25m, 10m, 2.0m, 0.8m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5), FoodReplacementRule.Calories))).EnsureSuccessStatusCode();
        var adjusted = await coach.GetFromJsonAsync<EnergyCalculatorResponse>($"/api/v1/clients/{client.Id}/nutrition-targets");
        Assert.Equal((1.6m, -25m, 2136m), (adjusted!.ActivityFactor, adjusted.AdjustmentPercent, adjusted.GoalCalories));

        // The client's own macro mode (§15) is stored on the profile.
        var percentages = new MacroPreferences(MacroMode.Percentages, ProteinPercent: 30, CarbohydratePercent: 40, FatPercent: 30);
        var saved = await ReadAsync<ClientProfileResponse>(await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile", Profile(birth, percentages)));
        Assert.Equal(percentages, saved.Macros);
        var split = await coach.GetFromJsonAsync<EnergyCalculatorResponse>($"/api/v1/clients/{client.Id}/nutrition-targets");
        Assert.Equal((MacroMode.Percentages, 160.2m, 213.6m), (split!.Macros.Mode, split.Protein, split.Carbohydrates));
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}/profile",
            Profile(birth, percentages with { FatPercent = 10 }))).StatusCode);

        // Another coach cannot read the client's targets; the calculator uses the caller's own settings.
        var other = await factory.CreateCoachClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/clients/{client.Id}/nutrition-targets")).StatusCode);
        var adHoc = await ReadAsync<EnergyCalculatorResponse>(await other.PostAsJsonAsync("/api/v1/nutrition-calculator/energy",
            new EnergyCalculatorInput(30, BiologicalSex.Male, 80, 180, ActivityLevel.ModeratelyActive, EnergyGoal.LoseWeight)));
        Assert.Equal(2207.2m, adHoc.GoalCalories);
        using var admin = factory.CreateAdminClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/nutrition-calculator/energy",
            new EnergyCalculatorInput(30, BiologicalSex.Male, 80, 180, ActivityLevel.ModeratelyActive, EnergyGoal.LoseWeight))).StatusCode);
    }

    [Fact]
    public async Task A_transferred_clients_plan_keeps_the_previous_coachs_custom_food()
    {
        var coachAId = await factory.CreateCoachAsync();
        var coachBId = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachAId);
        var coachB = factory.CreateCoachClient(coachBId);
        using var admin = factory.CreateAdminClient();
        var category = await CategoryAsync(admin);
        var custom = await ReadAsync<FoodResponse>(await coachA.PostAsJsonAsync("/api/v1/nutrition/foods", Food("A's bar", category)));
        var another = await ReadAsync<FoodResponse>(await coachA.PostAsJsonAsync("/api/v1/nutrition/foods", Food("A's other bar", category)));
        var client = await ReadAsync<ClientResponse>(await coachA.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Moving client", null, null, null)));
        var plan = await ReadAsync<DietPlanResponse>(await coachA.PostAsJsonAsync("/api/v1/diet-plans", Diet(custom.Id, 50) with { ClientId = client.Id }));
        (await admin.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachBId))).EnsureSuccessStatusCode();

        var moved = await coachB.GetFromJsonAsync<DietPlanResponse>($"/api/v1/diet-plans/{plan.Id}");
        Assert.Equal("A's bar", moved!.Variants[0].Meals[0].FoodItems[0].FoodNameEn);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/nutrition/foods/{custom.Id}")).StatusCode);

        // Coach B can keep editing the plan with the food it already has, but cannot add A's other foods.
        var edit = Diet(custom.Id, 80) with { ClientId = client.Id, BaseVersionNumber = 1 };
        Assert.Equal(HttpStatusCode.OK, (await coachB.PutAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions/current", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coachB.PutAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions/current",
            Diet(another.Id, 80) with { ClientId = client.Id })).StatusCode);
    }

    private static async Task<Guid> CategoryAsync(HttpClient admin) =>
        (await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Engine " + Guid.NewGuid().ToString("N"), null)))).Id;

    private static FoodInput Food(string name, Guid category) =>
        new(name, null, category, FoodBaseUnit.Gram, 400, 10, 60, 12, null, FiberPer100: 5, SugarPer100: 20, SodiumMgPer100: 150);

    private static async Task<PagedResult<FoodResponse>> ListAsync(HttpClient http, string query) =>
        (await http.GetFromJsonAsync<PagedResult<FoodResponse>>("/api/v1/nutrition/foods?" + query))!;

    private static DietPlanInput Diet(Guid foodId, decimal quantity, Guid? unitId = null) => new(
        "Engine plan", null, null, [],
        [new(Guid.NewGuid(), "Day", null, 0, true, null,
            [new(Guid.NewGuid(), "Breakfast", null, 0, null, [new(Guid.NewGuid(), foodId, quantity, 0, null, unitId)])], [])]);

    private static ClientProfileInput Profile(DateOnly birth, MacroPreferences? macros) => new(
        birth, Sex.Male, 180, 80, null, FitnessGoal.LoseFat, ActivityLevel.ModeratelyActive, 4, 8000, TrainingLocation.Gym,
        null, [], null, [], null, [], null, [], null, 4, null, null, null, macros);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
