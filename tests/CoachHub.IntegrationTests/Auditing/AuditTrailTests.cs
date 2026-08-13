using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CoachHub.API.Auditing;
using CoachHub.Application.Auditing;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using CoachHub.IntegrationTests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Auditing;

public sealed class AuditTrailTests
{
    [Fact]
    public async Task SaveChanges_captures_create_update_delete_and_enforces_append_only_rows()
    {
        var userId = Guid.NewGuid();
        var actor = new StubActorAccessor(new(
            AuditActorKind.Administrator,
            userId,
            "Coach"));
        var options = new DbContextOptionsBuilder<CoachHubDbContext>()
            .UseInMemoryDatabase("AuditTrail-" + Guid.NewGuid())
            .Options;
        await using var context = new CoachHubDbContext(options, actor);
        var package = Package.Create("Audited package", null, null);

        context.Add(package);
        await context.SaveChangesAsync();
        package.Update("Updated package", null, null, true);
        await context.SaveChangesAsync();
        context.Remove(package);
        await context.SaveChangesAsync();

        var entries = await context.Set<AuditEntry>()
            .Where(entry => entry.EntityType == nameof(Package) && entry.EntityId == package.Id)
            .OrderBy(entry => entry.OccurredAt)
            .ToArrayAsync();

        Assert.Equal(3, entries.Length);
        Assert.Equal(
            [AuditOperation.Create, AuditOperation.Update, AuditOperation.Delete],
            entries.Select(entry => entry.Operation));
        Assert.All(entries, entry =>
        {
            Assert.Equal(AuditActorKind.Administrator, entry.ActorKind);
            Assert.Equal(userId, entry.ActorUserId);
            Assert.Equal("Coach", entry.ActorDisplayName);
        });

        context.Remove(entries[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Administrator_can_filter_audit_entries_and_anonymous_access_is_rejected()
    {
        await using var factory = new CoachHubApiFactory();
        using var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/audit-entries")).StatusCode);

        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CoachHubApiFactory.AdminEmail,
            password = CoachHubApiFactory.AdminPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var packageName = "Audit " + Guid.NewGuid().ToString("N");
        var createResponse = await client.PostAsJsonAsync(
            "/api/reference-data/packages",
            new PackageInput(packageName, null, null, true));
        createResponse.EnsureSuccessStatusCode();
        var package = await createResponse.Content.ReadFromJsonAsync<PackageResponse>();
        Assert.NotNull(package);

        var page = await client.GetFromJsonAsync<PagedResult<AuditRecord>>(
            $"/api/audit-entries?entityType=Package&entityId={package.Id}&pageNumber=1&pageSize=10");

        Assert.NotNull(page);
        var entry = Assert.Single(page.Items);
        Assert.Equal("Create", entry.Operation);
        Assert.Equal("Administrator", entry.ActorKind);
        Assert.Equal(login.UserId, entry.ActorUserId);
        Assert.Equal(login.DisplayName, entry.ActorDisplayName);
    }

    [Fact]
    public void Http_actor_accessor_classifies_public_clients_without_storing_access_codes()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/client-forms/submissions";
        var accessor = new HttpAuditActorAccessor(new HttpContextAccessor { HttpContext = context });

        var actor = accessor.Current;

        Assert.Equal(AuditActorKind.PublicClient, actor.Kind);
        Assert.Null(actor.UserId);
        Assert.Null(actor.DisplayName);

        var userId = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, "Coach")
        ], "Bearer", JwtRegisteredClaimNames.Name, ClaimTypes.Role));

        actor = accessor.Current;
        Assert.Equal(AuditActorKind.Administrator, actor.Kind);
        Assert.Equal(userId, actor.UserId);
        Assert.Equal("Coach", actor.DisplayName);
    }

    private sealed class StubActorAccessor(AuditActor actor) : IAuditActorAccessor
    {
        public AuditActor Current => actor;
    }
}
