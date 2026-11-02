using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.FitnessTests;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.Domain.FitnessTests;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;
using CoachHub.IntegrationTests.WorkoutPlanning;

namespace CoachHub.IntegrationTests.Training;

/// <summary>
/// P20 on SQL Server: uploaded videos of exercises (§17) and tests (§34). A platform entry's video is platform media;
/// a coach's entry's video is in the coach's library, so the coach and their clients can play it and other coaches
/// cannot. Replacing, removing or deleting the entry removes the old file.
/// </summary>
public sealed class CatalogVideoTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    /// <summary>The start of an MP4 file: a box size, then "ftyp".</summary>
    private static readonly byte[] Mp4 = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0, 0, 2, 0];

    [Fact]
    public async Task Exercise_videos_follow_the_exercise_owner()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        // A stored account: the media row keeps its uploader.
        var admin = (await factory.CreateSignedInAccountAsync(AuthRoles.Administrator)).Http;
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoach = factory.CreateCoachClient(await factory.CreateCoachAsync());
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Viewer " + tag, null, null, null)));
        var portal = factory.CreateClientAreaClient(coachId, client.Id);
        var category = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/exercise-categories",
            new BilingualReferenceInput("Videos " + tag, null, true)));

        // A platform exercise: only an administrator uploads; the video is platform media every role can open.
        var platform = await ReadAsync<ExerciseResponse>(await admin.PostAsJsonAsync("/api/v1/training/exercises",
            new ExerciseInput("Platform squat " + tag, null, category.Id, null, null, PrimaryMuscleGroupId: TestWorkouts.Quadriceps)));
        Assert.Equal(HttpStatusCode.Forbidden, (await PutVideoAsync(coach, $"/api/v1/training/exercises/{platform.Id}/video", Mp4, "video/mp4")).StatusCode);
        var withVideo = await ReadAsync<ExerciseResponse>(await PutVideoAsync(admin, $"/api/v1/training/exercises/{platform.Id}/video", Mp4, "video/mp4"));
        var platformVideo = withVideo.VideoMediaId!.Value;
        Assert.Equal(HttpStatusCode.OK, (await otherCoach.GetAsync($"/api/v1/media/{platformVideo}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"/api/v1/media/{platformVideo}")).StatusCode);

        // Only MP4, WebM or MOV videos are accepted.
        Assert.Equal(HttpStatusCode.BadRequest, (await PutVideoAsync(admin, $"/api/v1/training/exercises/{platform.Id}/video", MeEndpointTests.Png, "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PutVideoAsync(admin, $"/api/v1/training/exercises/{platform.Id}/video", MeEndpointTests.Png, "video/mp4")).StatusCode);

        // A custom exercise: the video is in the coach's library; their client plays it, another coach cannot.
        var custom = await ReadAsync<ExerciseResponse>(await coach.PostAsJsonAsync("/api/v1/training/exercises",
            new ExerciseInput("Custom lunge " + tag, null, category.Id, null, null, PrimaryMuscleGroupId: TestWorkouts.Quadriceps)));
        var first = (await ReadAsync<ExerciseResponse>(await PutVideoAsync(coach, $"/api/v1/training/exercises/{custom.Id}/video", Mp4, "video/mp4"))).VideoMediaId!.Value;
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"/api/v1/media/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCoach.GetAsync($"/api/v1/media/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutVideoAsync(otherCoach, $"/api/v1/training/exercises/{custom.Id}/video", Mp4, "video/mp4")).StatusCode);

        // Replacing removes the old file; a file in use cannot be deleted on its own.
        var second = (await ReadAsync<ExerciseResponse>(await PutVideoAsync(coach, $"/api/v1/training/exercises/{custom.Id}/video", Mp4, "video/mp4"))).VideoMediaId!.Value;
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/media/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/media/{platformVideo}")).StatusCode);

        // Removing the video, or deleting the exercise, removes the file.
        var removed = await ReadAsync<ExerciseResponse>(await coach.DeleteAsync($"/api/v1/training/exercises/{custom.Id}/video"));
        Assert.Null(removed.VideoMediaId);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/media/{second}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/training/exercises/{platform.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/media/{platformVideo}")).StatusCode);
    }

    [Fact]
    public async Task Test_videos_are_in_the_coach_library_and_reach_the_client()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        using var admin = factory.CreateAdminClient();
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoach = factory.CreateCoachClient(await factory.CreateCoachAsync());
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Tester " + tag, null, null, null)));
        var portal = factory.CreateClientAreaClient(coachId, client.Id);

        var test = await ReadAsync<TestDefinitionResponse>(await coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Mobility, "Reach " + tag, null, TestUnit.Centimeters, true)));
        var video = (await ReadAsync<TestDefinitionResponse>(await PutVideoAsync(coach, $"/api/v1/fitness-tests/{test.Id}/video", Mp4, "video/mp4"))).VideoMediaId!.Value;
        Assert.Equal(HttpStatusCode.NotFound, (await otherCoach.GetAsync($"/api/v1/media/{video}")).StatusCode);

        // The client sees the video with the assigned test and can play it.
        (await coach.PutAsync($"/api/v1/clients/{client.Id}/fitness-tests/assignments/{test.Id}", null)).EnsureSuccessStatusCode();
        var mine = (await portal.GetFromJsonAsync<ClientTestsResponse>("/api/v1/me/fitness-tests"))!;
        Assert.Equal(video, Assert.Single(mine.Tests).Definition!.VideoMediaId);
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"/api/v1/media/{video}")).StatusCode);

        // Deleting the unused test removes its video.
        (await coach.DeleteAsync($"/api/v1/clients/{client.Id}/fitness-tests/assignments/{test.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await coach.DeleteAsync($"/api/v1/fitness-tests/{test.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/media/{video}")).StatusCode);
    }

    private static Task<HttpResponseMessage> PutVideoAsync(HttpClient http, string path, byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return http.PutAsync(path, new MultipartFormDataContent { { file, "file", "demo.mp4" } });
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
