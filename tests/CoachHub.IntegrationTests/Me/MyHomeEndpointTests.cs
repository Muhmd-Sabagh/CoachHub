using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.IntegrationTests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Me;

public sealed class MyHomeEndpointTests(CoachHubApiFactory factory) : IClassFixture<CoachHubApiFactory>
{
    [Fact]
    public async Task Client_role_without_a_linked_client_is_forbidden_not_a_server_error()
    {
        var token = factory.Services.GetRequiredService<ITokenIssuer>().Issue(new AuthenticatedUser(
            Guid.NewGuid(), "unlinked-client@coachhub.test", "Unlinked Client", [AuthRoles.Client], CoachId: null, ClientId: null), Guid.NewGuid());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/me/home", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        Assert.Equal("The account is not linked to a client.", problem.Detail);
    }
}
