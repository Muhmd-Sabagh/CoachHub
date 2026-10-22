using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.SavedPlans;

public sealed class SavedPlanQueryEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;
    public SavedPlanQueryEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _admin = factory.CreateAdminClient();
    }

    [Fact]
    public async Task Administrator_can_page_sort_and_filter_combined_plan_summaries()
    {
        await AuthenticateAsync();
        var client = await PostAsync<ClientResponse>("/api/v1/clients", new ClientCreateInput("Query Client", null, null, null));
        var category = await AdminPostAsync<BilingualReferenceResponse>("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Query foods " + Guid.NewGuid().ToString("N"), null));
        var food = await AdminPostAsync<FoodResponse>("/api/v1/nutrition/foods",
            new FoodInput("Query food", null, category.Id, FoodBaseUnit.Gram, 100, 10, 20, 5, null));

        var mealId = Guid.NewGuid();
        var diet = await PostAsync<DietPlanResponse>("/api/v1/diet-plans", new DietPlanInput(
            "Alpha Diet", null, client.Id, [],
            [new(Guid.NewGuid(), "Standard", null, 0, true, null,
                [new(mealId, "Meal", null, 0, null,
                    [new(Guid.NewGuid(), food.Id, 150, 0, null)])], [])]));
        var workout = await PostAsync<WorkoutPlanResponse>("/api/v1/workout-plans", new WorkoutPlanInput(
            "Beta Workout", "تمرين", client.Id, [],
            [
                new(Guid.NewGuid(), "Day One", null, null, null, 0, []),
                new(Guid.NewGuid(), "Day Two", null, null, null, 1, [])
            ]));

        var firstPage = await _client.GetFromJsonAsync<PagedResult<SavedPlanSummary>>(
            "/api/v1/saved-plans?pageNumber=1&pageSize=1&sortBy=name&sortDescending=false");
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Single(firstPage.Items);
        Assert.Equal(diet.Id, firstPage.Items[0].Id);

        var dietPage = await GetAsync("planName=Alpha&planType=Diet&minCalories=149&maxCalories=151&minProtein=14&maxCarbohydrates=31&isAssigned=true");
        var dietSummary = Assert.Single(dietPage.Items);
        Assert.Equal(150m, dietSummary.TotalWeight);
        Assert.Equal(150m, dietSummary.TotalCalories);
        Assert.Equal(15m, dietSummary.TotalProtein);
        Assert.Equal(30m, dietSummary.TotalCarbohydrates);
        Assert.Equal(7.5m, dietSummary.TotalFat);
        Assert.Null(dietSummary.WorkoutDayCount);
        Assert.Equal(client.Id, dietSummary.ClientId);

        var workoutPage = await GetAsync("clientName=Query&clientId=" + client.Id + "&planType=Workout&minWorkoutDays=2&maxWorkoutDays=2");
        var workoutSummary = Assert.Single(workoutPage.Items);
        Assert.Equal(workout.Id, workoutSummary.Id);
        Assert.Equal(2, workoutSummary.WorkoutDayCount);
        Assert.Null(workoutSummary.TotalCalories);
        Assert.Equal("تمرين", workoutSummary.NameAr);

        var invalid = await _client.GetAsync("/api/v1/saved-plans?minCalories=1&minWorkoutDays=1");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Saved_plan_query_rejects_anonymous_access() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/saved-plans")).StatusCode);

    private Task<PagedResult<SavedPlanSummary>> GetAsync(string query) =>
        _client.GetFromJsonAsync<PagedResult<SavedPlanSummary>>("/api/v1/saved-plans?" + query)!;

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
        var response = await _client.PostAsJsonAsync(uri, input); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>Platform catalog setup (foods, exercises, categories) is administrator work.</summary>
    private async Task<T> AdminPostAsync<T>(string uri, object input)
    {
        var response = await _admin.PostAsJsonAsync(uri, input); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
