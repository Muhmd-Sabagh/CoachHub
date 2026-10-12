using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Models;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.Domain.PlanDelivery;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// Coach-as-tenant isolation (D1, §56, §62), client scoping, and transfers (D7), proven on SQL
/// Server where the global query filters and constraints actually run.
/// </summary>
public sealed class TenantIsolationTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task A_coach_cannot_see_or_change_another_coachs_clients_plans_or_packages()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachB = await factory.CreateCoachClientAsync();
        var clientOfA = await CreateClientAsync(coachA, "Isolated client");
        var planOfA = await CreateWorkoutPlanAsync(coachA, clientOfA.Id);
        var packageOfA = await CreatePackageAsync(coachA);

        var visibleToB = await coachB.GetFromJsonAsync<PagedResult<ClientResponse>>("/api/v1/clients?pageSize=100");
        Assert.DoesNotContain(visibleToB!.Items, client => client.Id == clientOfA.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/clients/{clientOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.PutAsJsonAsync($"/api/v1/clients/{clientOfA.Id}",
            new ClientUpdateInput("Taken over", null, null, PlanWorkflowStatus.NotStarted, PlanWorkflowStatus.NotStarted, true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.DeleteAsync($"/api/v1/clients/{clientOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/workout-plans/{planOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.DeleteAsync($"/api/v1/workout-plans/{planOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/reference-data/packages/{packageOfA.Id}")).StatusCode);

        var crossAssignment = await coachB.PostAsJsonAsync("/api/v1/workout-plans", WorkoutPlan(clientOfA.Id));
        Assert.Equal(HttpStatusCode.BadRequest, crossAssignment.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await coachA.GetAsync($"/api/v1/clients/{clientOfA.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_administrator_sees_every_coach_and_must_name_the_coach_for_new_clients()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachB = await factory.CreateCoachClientAsync();
        var admin = factory.CreateAdminClient();
        var clientOfA = await CreateClientAsync(coachA, "Admin visible A");
        var clientOfB = await CreateClientAsync(coachB, "Admin visible B");

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/clients/{clientOfA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/clients/{clientOfB.Id}")).StatusCode);

        var withoutCoach = await admin.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("No coach", null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, withoutCoach.StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Assigned by admin", null, null, null, clientOfB.CoachId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(clientOfB.CoachId, (await created.Content.ReadFromJsonAsync<ClientResponse>())!.CoachId);
    }

    [Fact]
    public async Task A_client_sees_only_their_own_portal_data()
    {
        var coach = await factory.CreateCoachClientAsync();
        var mine = await CreateClientAsync(coach, "Portal owner");
        var other = await CreateClientAsync(coach, "Portal neighbour");
        var portal = factory.CreateClientPortalClient(mine.CoachId, mine.Id);

        var overview = await portal.GetFromJsonAsync<API.ClientPortal.ClientPortalResponse>("/api/v1/client-portal/overview");
        Assert.Equal(mine.Id, overview!.Client.Client.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync($"/api/v1/clients/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/clients")).StatusCode);
    }

    [Fact]
    public async Task Transfer_moves_coaching_records_and_keeps_commercial_history_with_the_selling_coach()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachBId = await factory.CreateCoachAsync();
        var coachB = factory.CreateCoachClient(coachBId);
        var admin = factory.CreateAdminClient();
        var client = await CreateClientAsync(coachA, "Transferred client");
        var plan = await CreateWorkoutPlanAsync(coachA, client.Id);
        var delivered = await coachA.PostAsJsonAsync("/api/v1/plan-deliveries", new DeliveredPlanInput(
            client.Id, DeliveredPlanType.Workout, plan.Id, null, null, "en", DeliveryChannel.Download, null));
        Assert.Equal(HttpStatusCode.Created, delivered.StatusCode);
        var subscription = await SellSubscriptionAsync(coachA, admin, client.Id);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await coachA.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachBId))).StatusCode);
        var transfer = await admin.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachBId));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(coachBId, (await transfer.Content.ReadFromJsonAsync<ClientResponse>())!.CoachId);

        // Coaching records followed the client.
        var detail = await coachB.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.OK, (await coachB.GetAsync($"/api/v1/workout-plans/{plan.Id}")).StatusCode);
        var deliveries = await coachB.GetFromJsonAsync<DeliveredPlanResponse[]>($"/api/v1/plan-deliveries?clientId={client.Id}");
        Assert.Single(deliveries!);

        // Commercial history stayed with the coach who sold it; the new coach starts afresh.
        Assert.DoesNotContain(detail!.Subscriptions, item => item.Id == subscription.Id);
        Assert.Equal(SubscriptionStatus.Inactive, detail.Client.SubscriptionStatus);
        Assert.Equal(HttpStatusCode.NotFound, (await coachA.GetAsync($"/api/v1/clients/{client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachA.GetAsync($"/api/v1/workout-plans/{plan.Id}")).StatusCode);

        // The old coach's books protect the client from deletion by the new coach.
        Assert.Equal(HttpStatusCode.Conflict, (await coachB.DeleteAsync($"/api/v1/clients/{client.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_created_coach_can_log_in_by_user_name_and_is_blocked_when_deactivated()
    {
        var admin = factory.CreateAdminClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await admin.PostAsJsonAsync("/api/v1/admin/coaches", new CoachCreateInput(
            "Coach " + suffix, $"coach.{suffix}@coachhub.test", "coach" + suffix, "CoachPassword!123"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var coach = (await create.Content.ReadFromJsonAsync<CoachResponse>())!;
        Assert.Equal("coach" + suffix, coach.UserName);

        var anonymous = factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new { login = "coach" + suffix, password = "CoachPassword!123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = await login.Content.ReadFromJsonAsync<Application.Auth.Login.LoginResult>();
        Assert.Equal(coach.Id, result!.CoachId);
        Assert.Contains("Coach", result.Roles);

        var settings = await factory.CreateCoachClient(coach.Id).GetFromJsonAsync<CoachHub.Application.Settings.WorkspaceSettingsResponse>("/api/v1/settings");
        Assert.Equal("Coach " + suffix, settings!.CoachName);

        (await admin.PutAsJsonAsync($"/api/v1/admin/coaches/{coach.Id}", new CoachUpdateInput("Coach " + suffix, false)))
            .EnsureSuccessStatusCode();
        var blocked = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new { login = $"coach.{suffix}@coachhub.test", password = "CoachPassword!123" });
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
    }

    [Fact]
    public async Task Coaches_cannot_reach_platform_administration()
    {
        var coach = await factory.CreateCoachClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await coach.GetAsync("/api/v1/admin/coaches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coach.GetAsync("/api/v1/accounts")).StatusCode);
    }

    private static async Task<ClientResponse> CreateClientAsync(HttpClient coach, string name)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput(name + " " + Guid.NewGuid().ToString("N")[..6], null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private static WorkoutPlanInput WorkoutPlan(Guid? clientId) => new(
        "Plan " + Guid.NewGuid().ToString("N")[..6], null, clientId, [],
        [new WorkoutDayInput(Guid.NewGuid(), "Day 1", null, null, null, 0, [])]);

    private static async Task<WorkoutPlanResponse> CreateWorkoutPlanAsync(HttpClient coach, Guid? clientId)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/workout-plans", WorkoutPlan(clientId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkoutPlanResponse>())!;
    }

    private static async Task<PackageResponse> CreatePackageAsync(HttpClient coach)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/reference-data/packages",
            new PackageInput("Package " + Guid.NewGuid().ToString("N"), null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PackageResponse>())!;
    }

    private static async Task<SubscriptionResponse> SellSubscriptionAsync(HttpClient coach, HttpClient admin, Guid clientId)
    {
        var package = await CreatePackageAsync(coach);
        var currencyResponse = await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("T" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(), "Test currency", null));
        currencyResponse.EnsureSuccessStatusCode();
        var currency = (await currencyResponse.Content.ReadFromJsonAsync<CurrencyResponse>())!;
        var response = await coach.PostAsJsonAsync($"/api/v1/clients/{clientId}/subscriptions", new SubscriptionInput(
            package.Id, DateOnly.FromDateTime(DateTime.UtcNow), 1, 100m, currency.Id, null, 0));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>())!;
    }
}
