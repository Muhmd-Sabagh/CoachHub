using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Pdf;

public sealed class PlanPdfEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;
    public PlanPdfEndpointTests(CoachHubApiFactory factory) =>
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Preview_and_download_generate_english_and_arabic_pdfs_without_server_files()
    {
        await AuthenticateAsync();
        var foodCategory = await PostAsync<BilingualReferenceResponse>("/api/reference-data/food-categories",
            new BilingualReferenceInput("PDF food " + Guid.NewGuid().ToString("N"), null));
        var food = await PostAsync<FoodResponse>("/api/nutrition/foods",
            new FoodInput("Oats", "شوفان", foodCategory.Id, "g", 389, 16.9m, 66.3m, 6.9m, null));
        var exerciseCategory = await PostAsync<BilingualReferenceResponse>("/api/reference-data/exercise-categories",
            new BilingualReferenceInput("PDF exercise " + Guid.NewGuid().ToString("N"), null));
        var press = await PostAsync<ExerciseResponse>("/api/training/exercises",
            new ExerciseInput("Bench Press", "ضغط الصدر", exerciseCategory.Id, null, "https://youtu.be/example", true));

        var mealId = Guid.NewGuid();
        var diet = await PostAsync<DietPlanResponse>("/api/diet-plans", new DietPlanInput(
            "Performance Diet", "خطة الأداء", null,
            [new(Guid.NewGuid(), "Visible coach note", 0, true), new(Guid.NewGuid(), "Hidden coach note", 1, false)],
            [
                new(Guid.NewGuid(), "Training day", "يوم التدريب", 0, true, "Active version note",
                    [new(mealId, "Breakfast", "الإفطار", 0, "Before training",
                        [new(Guid.NewGuid(), food.Id, 125, 0, "Slow release")])], []),
                new(Guid.NewGuid(), "Hidden version", "نسخة مخفية", 1, false, null,
                    [new(Guid.NewGuid(), "Hidden meal", null, 0, null, [])], [])
            ]));
        var workout = await PostAsync<WorkoutPlanResponse>("/api/workout-plans", new WorkoutPlanInput(
            "Strength Builder", "بناء القوة", null,
            [new(Guid.NewGuid(), "Visible workout note", 0, true), new(Guid.NewGuid(), "Hidden workout note", 1, false)],
            [new(Guid.NewGuid(), "Push Day", "يوم الدفع", "Upper body", "Warm up", 0,
                [
                    new(Guid.NewGuid(), press.Id, 0, "3", "8-12", "90s", null, "RPE 8", "Controlled"),
                    new(Guid.NewGuid(), press.Id, 1, null, null, null, null, null, null)
                ])]));

        var serverPdfFilesBefore = Directory.GetFiles(AppContext.BaseDirectory, "*.pdf", SearchOption.AllDirectories);
        var dietPreview = await _client.GetAsync($"/api/diet-plans/{diet.Id}/pdf/preview?language=English");
        await AssertPdfAsync(dietPreview, "inline", "Performance-Diet-diet-plan-en.pdf");
        var dietArabic = await _client.GetAsync($"/api/diet-plans/{diet.Id}/pdf/download?language=Arabic");
        await AssertPdfAsync(dietArabic, "attachment", "Performance-Diet-diet-plan-ar.pdf");
        var workoutPreview = await _client.GetAsync($"/api/workout-plans/{workout.Id}/pdf/preview?language=English");
        await AssertPdfAsync(workoutPreview, "inline", "Strength-Builder-workout-plan-en.pdf");
        var workoutArabic = await _client.GetAsync($"/api/workout-plans/{workout.Id}/pdf/download?language=Arabic");
        await AssertPdfAsync(workoutArabic, "attachment", "Strength-Builder-workout-plan-ar.pdf");
        Assert.Equal(serverPdfFilesBefore, Directory.GetFiles(AppContext.BaseDirectory, "*.pdf", SearchOption.AllDirectories));

        var qaOutput = Environment.GetEnvironmentVariable("COACHHUB_PDF_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutput))
        {
            Directory.CreateDirectory(qaOutput);
            await File.WriteAllBytesAsync(Path.Combine(qaOutput, "diet-english.pdf"), await dietPreview.Content.ReadAsByteArrayAsync());
            await File.WriteAllBytesAsync(Path.Combine(qaOutput, "diet-arabic.pdf"), await dietArabic.Content.ReadAsByteArrayAsync());
            await File.WriteAllBytesAsync(Path.Combine(qaOutput, "workout-english.pdf"), await workoutPreview.Content.ReadAsByteArrayAsync());
            await File.WriteAllBytesAsync(Path.Combine(qaOutput, "workout-arabic.pdf"), await workoutArabic.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Pdf_endpoints_reject_anonymous_access() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync($"/api/diet-plans/{Guid.NewGuid()}/pdf/preview")).StatusCode);

    private static async Task AssertPdfAsync(HttpResponseMessage response, string disposition, string fileName)
    {
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(disposition, response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains(fileName, response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
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
        var response = await _client.PostAsJsonAsync(uri, input); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
