using System.Net.Http.Headers;
using CoachHub.Application.Auth;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Auth;

/// <summary>
/// Signed-in HTTP clients for each role. Tokens are issued directly, so tests are not throttled by
/// the login rate limit; login itself is covered by <see cref="AuthenticationEndpointTests"/>.
/// </summary>
public static class TestIdentities
{
    public static HttpClient CreateAdminClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClientFor(new AuthenticatedUser(
            Guid.NewGuid(), CoachHubApiFactory.AdminEmail, "CoachHub Administrator",
            [AuthRoles.Administrator], CoachId: null, ClientId: null));

    /// <summary>Creates a new coach (tenant) and returns a client signed in as that coach.</summary>
    public static async Task<HttpClient> CreateCoachClientAsync(
        this WebApplicationFactory<Program> factory, string? displayName = null)
    {
        var coachId = await factory.CreateCoachAsync(displayName);
        return factory.CreateCoachClient(coachId);
    }

    public static HttpClient CreateCoachClient(this WebApplicationFactory<Program> factory, Guid coachId) =>
        factory.CreateClientFor(new AuthenticatedUser(
            Guid.NewGuid(), $"coach.{coachId:N}@coachhub.test", "Test Coach",
            [AuthRoles.Coach], coachId, ClientId: null));

    public static HttpClient CreateClientPortalClient(
        this WebApplicationFactory<Program> factory, Guid coachId, Guid clientId) =>
        factory.CreateClientFor(new AuthenticatedUser(
            Guid.NewGuid(), $"client.{clientId:N}@coachhub.test", "Test Client",
            [AuthRoles.Client], coachId, clientId));

    public static async Task<Guid> CreateCoachAsync(
        this WebApplicationFactory<Program> factory, string? displayName = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        var coach = Coach.Create(displayName ?? "Coach " + Guid.NewGuid().ToString("N")[..8], DateTimeOffset.UtcNow);
        db.Add(coach);
        await db.SaveChangesAsync();
        return coach.Id;
    }

    public static HttpClient CreateClientFor(this WebApplicationFactory<Program> factory, AuthenticatedUser user)
    {
        var token = factory.Services.GetRequiredService<ITokenIssuer>().Issue(user);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
