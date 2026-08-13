using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Nutrition;

public sealed class NutritionEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly HttpClient _client;

    public NutritionEndpointTests(CoachHubApiFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Administrator_can_manage_filter_and_page_foods_with_media()
    {
        await AuthenticateAsync();
        var category = await CreateCategoryAsync("Protein " + Guid.NewGuid().ToString("N"));
        var media = await UploadImageAsync();
        var prefix = "Phase6-" + Guid.NewGuid().ToString("N");

        var first = await CreateFoodAsync(
            new FoodInput(prefix + " Chicken", null, category.Id, "gram", 165, 31, 0, 3.6m, media.Id, true));
        await CreateFoodAsync(
            new FoodInput(prefix + " Turkey", "ديك رومي", category.Id, "gram", 135, 30, 0, 1, null, true));
        await CreateFoodAsync(
            new FoodInput("Unrelated " + prefix, null, category.Id, "piece", 50, 1, 5, 2, null, false));

        var page = await _client.GetFromJsonAsync<PagedResult<FoodResponse>>(
            $"/api/nutrition/foods?searchTerm={prefix}&categoryId={category.Id}&isActive=true&pageNumber=1&pageSize=1&sortBy=protein&sortDescending=true");
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(first.Id, page.Items[0].Id);
        Assert.Equal(media.Id, page.Items[0].MediaId);

        var update = await _client.PutAsJsonAsync(
            $"/api/nutrition/foods/{first.Id}",
            new FoodInput(prefix + " Chicken", "دجاج", category.Id, "gram", 165, 31, 0, 3.6m, media.Id, false));
        update.EnsureSuccessStatusCode();
        Assert.False((await update.Content.ReadFromJsonAsync<FoodResponse>())!.IsActive);

        var categoryDelete = await _client.DeleteAsync($"/api/reference-data/food-categories/{category.Id}");
        Assert.Equal(HttpStatusCode.Conflict, categoryDelete.StatusCode);

        var delete = await _client.DeleteAsync($"/api/nutrition/foods/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Legacy_import_is_idempotent_and_preserves_bilingual_category_mapping()
    {
        await AuthenticateAsync();
        var legacyId = Random.Shared.Next(100000, int.MaxValue);
        var categoryName = "Legacy Grains " + Guid.NewGuid().ToString("N");
        var rows = new[]
        {
            new LegacyFoodImportRow(legacyId, "Legacy Oats", "gram", 389, 16.9m, 66.3m, 6.9m, "/images/oats.jpg", null, "شوفان", categoryName),
            new LegacyFoodImportRow(legacyId + 1, "Invalid", "gram", -1, 0, 0, 0, null, null)
        };

        var response = await _client.PostAsJsonAsync("/api/nutrition/foods/legacy-import", rows);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LegacyFoodImportResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.InvalidCount);
        Assert.Contains(result.Rows[0].Messages, message => message.Contains("not retained"));

        var repeated = await _client.PostAsJsonAsync("/api/nutrition/foods/legacy-import", new[] { rows[0] });
        repeated.EnsureSuccessStatusCode();
        var repeatResult = await repeated.Content.ReadFromJsonAsync<LegacyFoodImportResult>();
        Assert.Equal(1, repeatResult!.AlreadyImportedCount);

        var categories = await _client.GetFromJsonAsync<PagedResult<BilingualReferenceResponse>>(
            "/api/reference-data/food-categories?searchTerm=" + Uri.EscapeDataString(categoryName) + "&pageSize=100");
        Assert.NotNull(categories);
        Assert.Single(categories.Items, item => item.NameEn == categoryName);
        var imported = await _client.GetFromJsonAsync<FoodResponse>(
            $"/api/nutrition/foods/{result.Rows[0].FoodItemId}");
        Assert.Equal("شوفان", imported!.NameAr);
    }

    [Fact]
    public async Task Food_endpoints_reject_anonymous_access()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/nutrition/foods")).StatusCode);
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
            "/api/reference-data/food-categories",
            new BilingualReferenceInput(name, null, true));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BilingualReferenceResponse>())!;
    }

    private async Task<MediaMetadata> UploadImageAsync()
    {
        using var form = new MultipartFormDataContent();
        using var bytes = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "food.png");
        var response = await _client.PostAsync("/api/media", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaMetadata>())!;
    }

    private async Task<FoodResponse> CreateFoodAsync(FoodInput input)
    {
        var response = await _client.PostAsJsonAsync("/api/nutrition/foods", input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FoodResponse>())!;
    }
}