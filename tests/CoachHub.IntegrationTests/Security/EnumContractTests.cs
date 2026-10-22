using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using CoachHub.Application.Clients;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Security;

/// <summary>
/// Enums cross the API as names, which is what the Angular client sends and compares. Each enum
/// declares its converter so the API, these tests and any other .NET caller agree.
/// </summary>
public sealed class EnumContractTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    [Fact]
    public void Every_enum_declares_name_based_json()
    {
        var assemblies = new[] { typeof(CoachHub.Domain.Common.Entity).Assembly, typeof(ClientService).Assembly };
        var missing = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsEnum && type.IsPublic && type.GetCustomAttribute<FlagsAttribute>() is null)
            .Where(type => type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType?.Name.StartsWith("JsonStringEnumConverter") != true)
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(missing.Length == 0, "Add [JsonConverter(typeof(JsonStringEnumConverter<T>))] to: " + string.Join(", ", missing));
    }

    [Fact]
    public async Task Responses_carry_enum_names_and_requests_accept_them()
    {
        var coach = await factory.CreateCoachClientAsync();

        var created = await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Enum client", null, null, null));
        var json = await created.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"OnTrack\"", json);
        Assert.Contains("\"subscriptionStatus\":\"Inactive\"", json);

        var id = (await created.Content.ReadFromJsonAsync<ClientResponse>())!.Id;
        var update = await coach.PutAsync($"/api/v1/clients/{id}/status", JsonContent.Create(new { status = "NeedsReview" }));
        Assert.Contains("\"status\":\"NeedsReview\"", await update.Content.ReadAsStringAsync());
    }
}
