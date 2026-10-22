using System.Net.Http.Headers;
using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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

    public static HttpClient CreateClientAreaClient(
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

    public const string AccountPassword = "Account-Password!123";

    /// <summary>
    /// Creates a real login account and signs it in through <see cref="SessionService"/>, for
    /// tests of self-service endpoints that act on the stored account and its sessions.
    /// </summary>
    public static async Task<SignedInAccount> CreateSignedInAccountAsync(
        this WebApplicationFactory<Program> factory,
        string role,
        Guid? coachId = null,
        Guid? clientId = null,
        string userAgent = "Test device")
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var email = $"{role.ToLowerInvariant()}.{Guid.NewGuid():N}@coachhub.test";
        var user = new User
        {
            UserName = email, Email = email, EmailConfirmed = true, DisplayName = role + " account",
            IsActive = true, LockoutEnabled = true,
            CoachId = role == AuthRoles.Coach ? coachId : null,
            ClientId = role == AuthRoles.Client ? clientId : null
        };
        Ensure(await users.CreateAsync(user, AccountPassword));
        if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new Role { Name = role }));
        Ensure(await users.AddToRoleAsync(user, role));

        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        var tokens = await sessions.StartAsync(
            new AuthenticatedUser(user.Id, email, user.DisplayName, [role], coachId, clientId),
            new SessionClient(userAgent, "127.0.0.1"),
            CancellationToken.None);
        return new SignedInAccount(factory.WithBearer(tokens.Result.AccessToken), user.Id, email, tokens.RefreshToken!);
    }

    public static HttpClient WithBearer(this WebApplicationFactory<Program> factory, string accessToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    public static HttpClient CreateClientFor(this WebApplicationFactory<Program> factory, AuthenticatedUser user)
    {
        var token = factory.Services.GetRequiredService<ITokenIssuer>().Issue(user, Guid.NewGuid());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}

public sealed record SignedInAccount(HttpClient Http, Guid UserId, string Email, string RefreshToken);
