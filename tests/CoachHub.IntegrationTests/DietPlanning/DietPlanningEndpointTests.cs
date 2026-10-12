using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.DietPlanning;

public sealed class DietPlanningEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;
    public DietPlanningEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _admin = factory.CreateAdminClient();
    }

    [Fact]
    public async Task Administrator_can_create_reorder_assign_copy_and_calculate_complete_plan()
    {
        await AuthenticateAsync();
        var category = await AdminPostAsync<BilingualReferenceResponse>(
            "/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Diet planning " + Guid.NewGuid().ToString("N"), null));
        var oats = await AdminPostAsync<FoodResponse>("/api/v1/nutrition/foods",
            new FoodInput("Oats", "شوفان", category.Id, "gram", 100, 10, 20, 5, null));
        var eggs = await AdminPostAsync<FoodResponse>("/api/v1/nutrition/foods",
            new FoodInput("Eggs", null, category.Id, "gram", 200, 20, 5, 10, null));
        var client = await PostAsync<ClientResponse>("/api/v1/clients",
            new ClientCreateInput("Plan client", null, null, null));

        var noteId = Guid.NewGuid(); var versionId = Guid.NewGuid();
        var breakfastId = Guid.NewGuid(); var dinnerId = Guid.NewGuid();
        var oatsRowId = Guid.NewGuid(); var eggsRowId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var input = new DietPlanInput("Cutting", null, null,
            [new(noteId, "Drink water", 0, true)],
            [new(versionId, "Standard", "قياسي", 0, true, "PDF-ready",
                [
                    new(breakfastId, "Breakfast", null, 0, null,
                        [new(oatsRowId, oats.Id, 150, 0, null)]),
                    new(dinnerId, "Dinner", "عشاء", 1, null,
                        [new(eggsRowId, eggs.Id, 100, 0, null)])
                ],
                [new(groupId, breakfastId, oatsRowId, "Oat alternatives", 0,
                    [
                        new(Guid.NewGuid(), eggs.Id, null, 50, 0, null),
                        new(Guid.NewGuid(), null, dinnerId, null, 1, "whole meal")
                    ])])]);

        var created = await PostAsync<DietPlanResponse>("/api/v1/diet-plans", input);
        Assert.Null(created.NameAr);
        Assert.Equal(250m, created.Totals.Weight);
        Assert.Equal(350m, created.Totals.Calories);
        Assert.Equal(35m, created.Totals.Protein);
        Assert.Equal(35m, created.Totals.Carbohydrates);
        Assert.Equal(17.5m, created.Totals.Fat);
        Assert.Equal(100m, created.Variants[0].ReplacementGroups[0].Options[0].Totals.Calories);
        Assert.Equal(200m, created.Variants[0].ReplacementGroups[0].Options[1].Totals.Calories);

        var reordered = input with
        {
            ClientId = client.Id,
            Variants = [input.Variants.Single() with
            {
                Meals =
                [
                    input.Variants.Single().Meals.ElementAt(0) with { Order = 1 },
                    input.Variants.Single().Meals.ElementAt(1) with { Order = 0 }
                ]
            }]
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions/current", reordered);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<DietPlanResponse>())!;
        Assert.Equal(client.Id, updated.ClientId);
        Assert.Equal(dinnerId, updated.Variants[0].Meals[0].Id);

        var noteResponse = await _client.PutAsJsonAsync(
            $"/api/v1/diet-plans/{created.Id}/notes/{noteId}/active", new SetDietPlanNoteActiveInput(false));
        noteResponse.EnsureSuccessStatusCode();
        Assert.False((await noteResponse.Content.ReadFromJsonAsync<DietPlanResponse>())!.Notes[0].IsActive);

        var unassign = await _client.PutAsJsonAsync(
            $"/api/v1/diet-plans/{created.Id}/assignment", new AssignDietPlanInput(null));
        unassign.EnsureSuccessStatusCode();
        Assert.Null((await unassign.Content.ReadFromJsonAsync<DietPlanResponse>())!.ClientId);

        var copy = await PostAsync<DietPlanResponse>(
            $"/api/v1/diet-plans/{created.Id}/copies", new CopyDietPlanInput("Copied", null, client.Id));
        Assert.NotEqual(created.Id, copy.Id);
        Assert.NotEqual(versionId, copy.Variants[0].Id);
        Assert.Equal(client.Id, copy.ClientId);
        Assert.Equal(created.Totals, copy.Totals);

        var calculator = await PostAsync<EnergyCalculatorResponse>("/api/v1/nutrition-calculator/energy",
            new EnergyCalculatorInput(30, BiologicalSex.Male, 80, 180, 1.55m, EnergyGoal.LoseWeight));
        Assert.Equal(1780m, calculator.BasalMetabolicRate);
        Assert.Equal(2259m, calculator.GoalCalories);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/diet-plans/{copy.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/diet-plans/{copy.Id}")).StatusCode);
    }

    private System.Net.Http.Headers.AuthenticationHeaderValue? _coachAuthorization;

    /// <summary>
    /// Signs in as this test's coach (created on first use): tenant data is created in that
    /// coach's workspace, isolated from every other test.
    /// </summary>
    private async Task AuthenticateAsync()
    {
        _coachAuthorization ??= (await _factory.CreateCoachClientAsync()).DefaultRequestHeaders.Authorization;
        _client.DefaultRequestHeaders.Authorization = _coachAuthorization;
    }

    private async Task<T> PostAsync<T>(string uri, object input)
    {
        var response = await _client.PostAsJsonAsync(uri, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>Platform catalog setup (foods, exercises, categories) is administrator work.</summary>
    private async Task<T> AdminPostAsync<T>(string uri, object input)
    {
        var response = await _admin.PostAsJsonAsync(uri, input); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
