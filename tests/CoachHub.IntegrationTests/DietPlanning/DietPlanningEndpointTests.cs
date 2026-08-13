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
    private readonly HttpClient _client;
    public DietPlanningEndpointTests(CoachHubApiFactory factory) =>
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Administrator_can_create_reorder_assign_copy_and_calculate_complete_plan()
    {
        await AuthenticateAsync();
        var category = await PostAsync<BilingualReferenceResponse>(
            "/api/reference-data/food-categories",
            new BilingualReferenceInput("Diet planning " + Guid.NewGuid().ToString("N"), null));
        var oats = await PostAsync<FoodResponse>("/api/nutrition/foods",
            new FoodInput("Oats", "شوفان", category.Id, "gram", 100, 10, 20, 5, null));
        var eggs = await PostAsync<FoodResponse>("/api/nutrition/foods",
            new FoodInput("Eggs", null, category.Id, "gram", 200, 20, 5, 10, null));
        var client = await PostAsync<ClientResponse>("/api/clients",
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

        var created = await PostAsync<DietPlanResponse>("/api/diet-plans", input);
        Assert.Null(created.NameAr);
        Assert.Equal(250m, created.Totals.Weight);
        Assert.Equal(350m, created.Totals.Calories);
        Assert.Equal(35m, created.Totals.Protein);
        Assert.Equal(35m, created.Totals.Carbohydrates);
        Assert.Equal(17.5m, created.Totals.Fat);
        Assert.Equal(100m, created.Versions[0].ReplacementGroups[0].Options[0].Totals.Calories);
        Assert.Equal(200m, created.Versions[0].ReplacementGroups[0].Options[1].Totals.Calories);

        var reordered = input with
        {
            ClientId = client.Id,
            Versions = [input.Versions.Single() with
            {
                Meals =
                [
                    input.Versions.Single().Meals.ElementAt(0) with { Order = 1 },
                    input.Versions.Single().Meals.ElementAt(1) with { Order = 0 }
                ]
            }]
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/diet-plans/{created.Id}", reordered);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<DietPlanResponse>())!;
        Assert.Equal(client.Id, updated.ClientId);
        Assert.Equal(dinnerId, updated.Versions[0].Meals[0].Id);

        var noteResponse = await _client.PutAsJsonAsync(
            $"/api/diet-plans/{created.Id}/notes/{noteId}/active", new SetDietPlanNoteActiveInput(false));
        noteResponse.EnsureSuccessStatusCode();
        Assert.False((await noteResponse.Content.ReadFromJsonAsync<DietPlanResponse>())!.Notes[0].IsActive);

        var unassign = await _client.PutAsJsonAsync(
            $"/api/diet-plans/{created.Id}/assignment", new AssignDietPlanInput(null));
        unassign.EnsureSuccessStatusCode();
        Assert.Null((await unassign.Content.ReadFromJsonAsync<DietPlanResponse>())!.ClientId);

        var copy = await PostAsync<DietPlanResponse>(
            $"/api/diet-plans/{created.Id}/copies", new CopyDietPlanInput("Copied", null, client.Id));
        Assert.NotEqual(created.Id, copy.Id);
        Assert.NotEqual(versionId, copy.Versions[0].Id);
        Assert.Equal(client.Id, copy.ClientId);
        Assert.Equal(created.Totals, copy.Totals);

        var calculator = await PostAsync<EnergyCalculatorResponse>("/api/nutrition-calculator/energy",
            new EnergyCalculatorInput(30, BiologicalSex.Male, 80, 180, 1.55m, EnergyGoal.LoseWeight));
        Assert.Equal(1780m, calculator.BasalMetabolicRate);
        Assert.Equal(2259m, calculator.GoalCalories);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/diet-plans/{copy.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/diet-plans/{copy.Id}")).StatusCode);
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<T> PostAsync<T>(string uri, object input)
    {
        var response = await _client.PostAsJsonAsync(uri, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
