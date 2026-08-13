using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.WorkoutPlanning;

public sealed class WorkoutPlanningEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;
    public WorkoutPlanningEndpointTests(CoachHubApiFactory factory) =>
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Administrator_can_manage_reorder_copy_assign_notes_and_delete_workout_plan()
    {
        await AuthenticateAsync();
        var category = await PostAsync<BilingualReferenceResponse>("/api/reference-data/exercise-categories",
            new BilingualReferenceInput("Plan exercises " + Guid.NewGuid().ToString("N"), null));
        var press = await PostAsync<ExerciseResponse>("/api/training/exercises",
            new ExerciseInput("Bench Press", "ضغط صدر", category.Id, null, "https://youtu.be/example", true));
        var squat = await PostAsync<ExerciseResponse>("/api/training/exercises",
            new ExerciseInput("Squat", null, category.Id, null, null, true));
        var client = await PostAsync<ClientResponse>("/api/clients",
            new ClientCreateInput("Workout client", null, null, null));

        var noteId = Guid.NewGuid(); var pushId = Guid.NewGuid(); var legsId = Guid.NewGuid();
        var pressRowId = Guid.NewGuid(); var squatRowId = Guid.NewGuid();
        var input = new WorkoutPlanInput("Strength", null, null,
            [new(noteId, "Warm up first", 0, true)],
            [
                new(pushId, "Push", "دفع", "Upper body", "Control tempo", 0,
                    [new(pressRowId, press.Id, 0, "3-4", "8-12", "90-120s", "2-0-1-0", "RPE 8", "Pause")]),
                new(legsId, "Legs", null, null, null, 1,
                    [new(squatRowId, squat.Id, 0, "4", "6", "120s", null, "RIR 2", null)])
            ]);

        var created = await PostAsync<WorkoutPlanResponse>("/api/workout-plans", input);
        Assert.Null(created.NameAr);
        Assert.Equal("دفع", created.Days[0].NameAr);
        Assert.Equal("3-4", created.Days[0].Exercises[0].Sets);
        Assert.Equal("Bench Press", created.Days[0].Exercises[0].ExerciseNameEn);
        Assert.Equal(press.MediaId, created.Days[0].Exercises[0].MediaId);

        var reordered = input with
        {
            ClientId = client.Id,
            Days =
            [
                input.Days.ElementAt(0) with { Order = 1 },
                input.Days.ElementAt(1) with { Order = 0 }
            ]
        };
        var update = await _client.PutAsJsonAsync($"/api/workout-plans/{created.Id}", reordered);
        update.EnsureSuccessStatusCode();
        var updated = (await update.Content.ReadFromJsonAsync<WorkoutPlanResponse>())!;
        Assert.Equal(client.Id, updated.ClientId);
        Assert.Equal(legsId, updated.Days[0].Id);

        var note = await _client.PutAsJsonAsync($"/api/workout-plans/{created.Id}/notes/{noteId}/active",
            new SetWorkoutPlanNoteActiveInput(false));
        note.EnsureSuccessStatusCode();
        Assert.False((await note.Content.ReadFromJsonAsync<WorkoutPlanResponse>())!.Notes[0].IsActive);

        var unassign = await _client.PutAsJsonAsync($"/api/workout-plans/{created.Id}/assignment",
            new AssignWorkoutPlanInput(null));
        unassign.EnsureSuccessStatusCode();
        Assert.Null((await unassign.Content.ReadFromJsonAsync<WorkoutPlanResponse>())!.ClientId);

        var copy = await PostAsync<WorkoutPlanResponse>($"/api/workout-plans/{created.Id}/copies",
            new CopyWorkoutPlanInput("Strength copy", "نسخة", client.Id));
        Assert.NotEqual(created.Id, copy.Id);
        Assert.NotEqual(created.Days[0].Id, copy.Days[0].Id);
        Assert.Equal(client.Id, copy.ClientId);
        Assert.Equal("نسخة", copy.NameAr);

        var delete = await _client.DeleteAsync($"/api/workout-plans/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/workout-plans/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Workout_plan_endpoints_reject_anonymous_access() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/workout-plans/{Guid.NewGuid()}")).StatusCode);

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
