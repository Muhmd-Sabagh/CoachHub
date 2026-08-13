using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.SavedPlans;

public sealed class SavedPlanQueryEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;
    public SavedPlanQueryEndpointTests(CoachHubApiFactory factory) =>
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Administrator_can_page_sort_and_filter_combined_plan_summaries()
    {
        await AuthenticateAsync();
        var client = await PostAsync<ClientResponse>("/api/clients", new ClientCreateInput("Query Client", null, null, null));
        var category = await PostAsync<BilingualReferenceResponse>("/api/reference-data/food-categories",
            new BilingualReferenceInput("Query foods " + Guid.NewGuid().ToString("N"), null));
        var food = await PostAsync<FoodResponse>("/api/nutrition/foods",
            new FoodInput("Query food", null, category.Id, "gram", 100, 10, 20, 5, null));

        var mealId = Guid.NewGuid();
        var diet = await PostAsync<DietPlanResponse>("/api/diet-plans", new DietPlanInput(
            "Alpha Diet", null, client.Id, [],
            [new(Guid.NewGuid(), "Standard", null, 0, true, null,
                [new(mealId, "Meal", null, 0, null,
                    [new(Guid.NewGuid(), food.Id, 150, 0, null)])], [])]));
        var workout = await PostAsync<WorkoutPlanResponse>("/api/workout-plans", new WorkoutPlanInput(
            "Beta Workout", "تمرين", client.Id, [],
            [
                new(Guid.NewGuid(), "Day One", null, null, null, 0, []),
                new(Guid.NewGuid(), "Day Two", null, null, null, 1, [])
            ]));

        var firstPage = await _client.GetFromJsonAsync<PagedResult<SavedPlanSummary>>(
            "/api/saved-plans?pageNumber=1&pageSize=1&sortBy=name&sortDescending=false");
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
        Assert.Equal(client.ClientCode, dietSummary.ClientCode);

        var workoutPage = await GetAsync("clientName=Query&clientCode=" + client.ClientCode + "&planType=Workout&minWorkoutDays=2&maxWorkoutDays=2");
        var workoutSummary = Assert.Single(workoutPage.Items);
        Assert.Equal(workout.Id, workoutSummary.Id);
        Assert.Equal(2, workoutSummary.WorkoutDayCount);
        Assert.Null(workoutSummary.TotalCalories);
        Assert.Equal("تمرين", workoutSummary.NameAr);

        var invalid = await _client.GetAsync("/api/saved-plans?minCalories=1&minWorkoutDays=1");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Saved_plan_query_rejects_anonymous_access() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/saved-plans")).StatusCode);

    private Task<PagedResult<SavedPlanSummary>> GetAsync(string query) =>
        _client.GetFromJsonAsync<PagedResult<SavedPlanSummary>>("/api/saved-plans?" + query)!;

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
        var response = await _client.PostAsJsonAsync(uri, input); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
