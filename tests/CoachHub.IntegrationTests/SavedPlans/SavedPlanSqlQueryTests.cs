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
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.SavedPlans;

/// <summary>
/// The combined diet/workout list is one SQL UNION with filters and sorts on top. Before P6 it was only
/// tested on EF InMemory and failed to translate on SQL Server; every filter and sort runs here.
/// </summary>
public sealed class SavedPlanSqlQueryTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Every_filter_and_sort_translates_on_sql_server()
    {
        var coach = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var category = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Saved plans " + Guid.NewGuid().ToString("N"), null)));
        var food = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Saved food " + Guid.NewGuid().ToString("N")[..6], null, category.Id, FoodBaseUnit.Gram, 100, 10, 20, 5, null)));
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Listed client", null, null, null)));
        var diet = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", new DietPlanInput(
            "Alpha Diet", "حمية", client.Id, [],
            [new(Guid.NewGuid(), "Standard", null, 0, true, null,
                [new(Guid.NewGuid(), "Meal", null, 0, null, [new(Guid.NewGuid(), food.Id, 150, 0, null)])], [])])));
        var workout = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync("/api/v1/workout-plans", new WorkoutPlanInput(
            "Beta Workout", null, null, [],
            [new(Guid.NewGuid(), "Day One", null, null, null, 0, []), new(Guid.NewGuid(), "Day Two", null, null, null, 1, [])])));
        await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", new DietPlanInput(
            "Empty Diet", null, null, [],
            [new(Guid.NewGuid(), "Standard", null, 0, true, null, [new(Guid.NewGuid(), "Meal", null, 0, null, [])], [])])));

        string[] sorts = ["name", "clientname", "type", "createdat", "weight", "calories", "protein", "carbohydrates", "fat", "workoutdays"];
        foreach (var sort in sorts)
        {
            var page = await coach.GetFromJsonAsync<PagedResult<SavedPlanSummary>>($"/api/v1/saved-plans?sortBy={sort}&sortDescending=false");
            Assert.Equal(3, page!.TotalCount);
        }

        var dietRow = Assert.Single((await ListAsync(coach,
            "planName=حمية&clientName=Listed&clientId=" + client.Id + "&planType=Diet&isAssigned=true" +
            "&minCalories=149&maxCalories=151&minProtein=14&maxProtein=16&minCarbohydrates=29&maxCarbohydrates=31&minFat=7&maxFat=8")).Items);
        Assert.Equal((diet.Id, 150m, "Listed client", 1), (dietRow.Id, dietRow.TotalWeight!.Value, dietRow.ClientName, dietRow.VersionNumber!.Value));

        var workoutRow = Assert.Single((await ListAsync(coach, "planType=Workout&minWorkoutDays=2&maxWorkoutDays=2&isAssigned=false")).Items);
        Assert.Equal((workout.Id, 2, (string?)null, (decimal?)null), (workoutRow.Id, workoutRow.WorkoutDayCount!.Value, workoutRow.ClientName, workoutRow.TotalCalories));

        var empty = Assert.Single((await ListAsync(coach, "planName=Empty")).Items);
        Assert.Equal(0m, empty.TotalCalories);
        var today = DateTimeOffset.UtcNow;
        Assert.Equal(3, (await ListAsync(coach,
            $"createdFrom={Uri.EscapeDataString(today.AddDays(-1).ToString("O"))}&createdTo={Uri.EscapeDataString(today.AddDays(1).ToString("O"))}")).TotalCount);
    }

    private static async Task<PagedResult<SavedPlanSummary>> ListAsync(HttpClient coach, string query) =>
        (await coach.GetFromJsonAsync<PagedResult<SavedPlanSummary>>("/api/v1/saved-plans?" + query))!;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
