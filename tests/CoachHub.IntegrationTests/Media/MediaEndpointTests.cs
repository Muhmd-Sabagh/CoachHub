using System.Net;
using CoachHub.IntegrationTests.Auth;

namespace CoachHub.IntegrationTests.Media;

public sealed class MediaEndpointTests(CoachHubApiFactory factory) :
    IClassFixture<CoachHubApiFactory>
{
    [Fact]
    public async Task Anonymous_media_access_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/media/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}