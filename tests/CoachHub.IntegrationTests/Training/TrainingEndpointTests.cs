using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Training;

public sealed class TrainingEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;

    public TrainingEndpointTests(CoachHubApiFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Administrator_can_manage_filter_and_page_exercises_with_media()
    {
        await AuthenticateAsync();
        var category = await CreateCategoryAsync("Strength " + Guid.NewGuid().ToString("N"));
        var media = await UploadImageAsync();
        var prefix = "Phase7-" + Guid.NewGuid().ToString("N");

        var first = await CreateExerciseAsync(new ExerciseInput(
            prefix + " Squat", null, category.Id, media.Id, "https://youtu.be/squat", true));
        await CreateExerciseAsync(new ExerciseInput(
            prefix + " Press", "ضغط", category.Id, null, null, true));
        await CreateExerciseAsync(new ExerciseInput(
            "Unrelated " + prefix, null, category.Id, null, null, false));

        var page = await _client.GetFromJsonAsync<PagedResult<ExerciseResponse>>(
            $"/api/training/exercises?searchTerm={prefix}&categoryId={category.Id}&isActive=true&pageNumber=1&pageSize=1&sortBy=name");
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);

        var update = await _client.PutAsJsonAsync(
            $"/api/training/exercises/{first.Id}",
            new ExerciseInput(
                first.NameEn, "قرفصاء", category.Id, media.Id, first.YouTubeUrl, false));
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<ExerciseResponse>();
        Assert.False(updated!.IsActive);
        Assert.Equal("قرفصاء", updated.NameAr);

        var categoryDelete = await _client.DeleteAsync(
            $"/api/reference-data/exercise-categories/{category.Id}");
        Assert.Equal(HttpStatusCode.Conflict, categoryDelete.StatusCode);

        var invalid = await _client.PostAsJsonAsync(
            "/api/training/exercises",
            new ExerciseInput("Unsafe", null, category.Id, null, "https://example.com/video", true));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/training/exercises/{first.Id}")).StatusCode);
    }

    [Fact]
    public async Task Legacy_import_is_idempotent_and_uses_explicit_category_mapping()
    {
        await AuthenticateAsync();
        var legacyId = Random.Shared.Next(100000, int.MaxValue);
        var categoryName = "Legacy Strength " + Guid.NewGuid().ToString("N");
        var rows = new[]
        {
            new LegacyExerciseImportRow(
                legacyId,
                "Legacy Deadlift",
                "https://www.youtube.com/watch?v=deadlift",
                "/images/exercises/deadlift.jpg",
                null,
                null,
                categoryName),
            new LegacyExerciseImportRow(
                legacyId + 1,
                "Invalid Link",
                "https://example.com/video",
                null,
                null)
        };

        var response = await _client.PostAsJsonAsync(
            "/api/training/exercises/legacy-import",
            rows);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LegacyExerciseImportResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.InvalidCount);
        Assert.Contains(result.Rows[0].Messages, message => message.Contains("not retained"));

        var repeated = await _client.PostAsJsonAsync(
            "/api/training/exercises/legacy-import",
            new[] { rows[0] });
        var repeatResult = await repeated.Content.ReadFromJsonAsync<LegacyExerciseImportResult>();
        Assert.Equal(1, repeatResult!.AlreadyImportedCount);

        var categories = await _client.GetFromJsonAsync<PagedResult<BilingualReferenceResponse>>(
            "/api/reference-data/exercise-categories?searchTerm=" + Uri.EscapeDataString(categoryName) + "&pageSize=100");
        Assert.NotNull(categories);
        Assert.Single(categories.Items, item => item.NameEn == categoryName);
    }

    [Fact]
    public async Task Exercise_endpoints_reject_anonymous_access()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/training/exercises")).StatusCode);
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<BilingualReferenceResponse> CreateCategoryAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/reference-data/exercise-categories",
            new BilingualReferenceInput(name, null, true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BilingualReferenceResponse>())!;
    }

    private async Task<MediaMetadata> UploadImageAsync()
    {
        using var form = new MultipartFormDataContent();
        using var bytes = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "exercise.png");
        var response = await _client.PostAsync("/api/media", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaMetadata>())!;
    }

    private async Task<ExerciseResponse> CreateExerciseAsync(ExerciseInput input)
    {
        var response = await _client.PostAsJsonAsync("/api/training/exercises", input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExerciseResponse>())!;
    }
}