using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Login;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Localization;

/// <summary>
/// P19: errors come back in the request's language (<c>Accept-Language</c>) with their stable codes, and sign-in
/// returns the account's saved language.
/// </summary>
public sealed class LocalizationTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    [Fact]
    public async Task Validation_errors_follow_the_request_language_and_carry_their_codes()
    {
        var coachId = await factory.CreateCoachAsync();
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);
        var input = new UpdateMyProfileInput("", "letters", "xx");

        var arabic = await ProblemAsync(await SendAsync(account.Http, HttpMethod.Put, "/api/v1/me/profile", "ar", input), HttpStatusCode.BadRequest);
        Assert.Equal("تعذّر قبول البيانات", arabic.GetProperty("title").GetString());
        Assert.Equal("اختر الإنجليزية (en) أو العربية (ar).", arabic.GetProperty("errors").GetProperty("preferredLanguage")[0].GetString());
        Assert.Equal("account.language", arabic.GetProperty("errorCodes").GetProperty("preferredLanguage")[0].GetString());
        Assert.Equal("account.phone", arabic.GetProperty("errorCodes").GetProperty("phoneNumber")[0].GetString());

        // English, or no preference, keeps the thrown text and still names the code.
        foreach (var language in new[] { "en-GB,en;q=0.9", null })
        {
            var english = await ProblemAsync(await SendAsync(account.Http, HttpMethod.Put, "/api/v1/me/profile", language, input), HttpStatusCode.BadRequest);
            Assert.Equal("Validation failed", english.GetProperty("title").GetString());
            Assert.Equal("Choose English (en) or Arabic (ar).", english.GetProperty("errors").GetProperty("preferredLanguage")[0].GetString());
            Assert.Equal("account.language", english.GetProperty("errorCodes").GetProperty("preferredLanguage")[0].GetString());
        }
    }

    [Fact]
    public async Task The_framework_s_own_validation_is_localized_too()
    {
        // Empty sign-in fields fail model validation before any of our code runs.
        var problem = await ProblemAsync(await SendAsync(factory.CreateClient(), HttpMethod.Post, "/api/v1/auth/login", "en;q=0.4, ar-EG;q=0.8", new { login = "", password = "" }), HttpStatusCode.BadRequest);
        Assert.Equal("يوجد خطأ أو أكثر في البيانات المدخلة.", problem.GetProperty("title").GetString());
        Assert.Equal("هذا الحقل مطلوب.", problem.GetProperty("errors").GetProperty("Login")[0].GetString());
        Assert.Equal("problem.fieldRequired", problem.GetProperty("errorCodes").GetProperty("Login")[0].GetString());
    }

    [Fact]
    public async Task A_missing_resource_is_reported_in_Arabic_with_its_code()
    {
        var coach = await factory.CreateCoachClientAsync();
        var problem = await ProblemAsync(await SendAsync(coach, HttpMethod.Get, $"/api/v1/clients/{Guid.NewGuid()}", "ar"), HttpStatusCode.NotFound);
        Assert.Equal("غير موجود", problem.GetProperty("title").GetString());
        Assert.Equal("العنصر المطلوب غير موجود.", problem.GetProperty("detail").GetString());
        Assert.Equal("problem.notFoundDetail", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sign_in_returns_the_saved_language()
    {
        var coachId = await factory.CreateCoachAsync();
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);
        (await account.Http.PutAsJsonAsync("/api/v1/me/profile", new UpdateMyProfileInput("Coach", null, "ar"))).EnsureSuccessStatusCode();

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { login = account.Email, password = TestIdentities.AccountPassword });
        login.EnsureSuccessStatusCode();
        Assert.Equal("ar", (await login.Content.ReadFromJsonAsync<LoginResult>())!.PreferredLanguage);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient http, HttpMethod method, string path, string? language, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (language is not null) request.Headers.TryAddWithoutValidation("Accept-Language", language);
        if (body is not null) request.Content = JsonContent.Create(body);
        return http.SendAsync(request);
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }
}
