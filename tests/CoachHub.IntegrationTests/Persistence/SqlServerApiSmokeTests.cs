using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Auth.Login;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Persistence;

public sealed class SqlServerApiSmokeTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Administrator_can_login_and_round_trip_a_client_through_sql_server()
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword },
            CancellationToken.None);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>(CancellationToken.None);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var created = await client.PostAsJsonAsync(
            "/api/v1/clients",
            new { name = "SQL Server Client", email = "sql.client@coachhub.test" },
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/clients?searchTerm=SQL%20Server%20Client", CancellationToken.None);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt64());
    }
}
