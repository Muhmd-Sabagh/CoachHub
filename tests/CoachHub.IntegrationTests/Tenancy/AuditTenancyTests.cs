using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Auditing;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Clients;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// The audit trail over HTTP on SQL Server (D2, §58): real roles, previous values, tenant
/// isolation for coaches, and history that survives transfers and deletions.
/// </summary>
public sealed class AuditTenancyTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task A_coach_edit_records_the_coach_the_client_and_the_previous_values()
    {
        var coachId = await factory.CreateCoachAsync();
        var coach = factory.CreateCoachClient(coachId);
        var client = await CreateClientAsync(coach, "Before edit");

        var updated = await coach.PutAsJsonAsync($"/api/v1/clients/{client.Id}", new ClientUpdateInput(
            "After edit", null, null, true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var entry = Assert.Single((await AuditAsync(coach, $"entityType=Client&entityId={client.Id}&operation=Update")).Items);
        Assert.Equal("Coach", entry.ActorKind);
        Assert.Equal(coachId, entry.CoachId);
        Assert.Equal(client.Id, entry.ClientId);
        var name = Assert.Single(entry.Changes, change => change.Property == "Name");
        Assert.Equal(client.Name, name.From!.Value.GetString());
        Assert.Equal("After edit", name.To!.Value.GetString());
    }

    [Fact]
    public async Task A_coach_reads_only_their_own_tenants_trail_and_administrators_read_all()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachB = await factory.CreateCoachClientAsync();
        var clientOfA = await CreateClientAsync(coachA, "Private history");

        Assert.Empty((await AuditAsync(coachB, $"clientId={clientOfA.Id}")).Items);
        Assert.NotEmpty((await AuditAsync(coachA, $"clientId={clientOfA.Id}")).Items);
        Assert.NotEmpty((await AuditAsync(factory.CreateAdminClient(), $"clientId={clientOfA.Id}")).Items);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await factory.CreateClientAreaClient(clientOfA.CoachId, clientOfA.Id).GetAsync("/api/v1/audit-entries")).StatusCode);
    }

    [Fact]
    public async Task Changes_to_aggregate_children_carry_the_aggregates_coach_and_client()
    {
        var coachId = await factory.CreateCoachAsync();
        var coach = factory.CreateCoachClient(coachId);
        var client = await CreateClientAsync(coach, "Plan owner");

        var plan = await coach.PostAsJsonAsync("/api/v1/workout-plans", new WorkoutPlanInput(
            "Strength " + Guid.NewGuid().ToString("N")[..6], null, client.Id, [],
            [new WorkoutDayInput(Guid.NewGuid(), "Day 1", null, null, null, 0, [])]));
        Assert.Equal(HttpStatusCode.Created, plan.StatusCode);

        var day = Assert.Single((await AuditAsync(coach, $"entityType=WorkoutDay&clientId={client.Id}")).Items);
        Assert.Equal(coachId, day.CoachId);
        Assert.Equal(client.Id, day.ClientId);
    }

    [Fact]
    public async Task A_transfer_is_recorded_with_what_moved_and_a_deleted_clients_history_remains()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachIdB = await factory.CreateCoachAsync();
        var admin = factory.CreateAdminClient();
        var client = await CreateClientAsync(coachA, "Moving client");

        var transfer = await admin.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachIdB));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        var recorded = Assert.Single((await AuditAsync(admin, $"entityType=ClientTransfer&entityId={client.Id}")).Items);
        Assert.Equal("Administrator", recorded.ActorKind);
        Assert.Equal(coachIdB, recorded.CoachId);
        Assert.Equal(client.CoachId, Assert.Single(recorded.Changes, change => change.Property == "CoachId").From!.Value.GetGuid());
        Assert.Contains(recorded.Changes, change => change.Property == "WorkoutPlansMoved");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{client.Id}")).StatusCode);
        var deletion = Assert.Single((await AuditAsync(admin, $"entityType=Client&entityId={client.Id}&operation=Delete")).Items);
        Assert.Equal("Moving client", Assert.Single(deletion.Changes, change => change.Property == "Name").From!.Value.GetString()![..13]);
        Assert.True((await AuditAsync(admin, $"clientId={client.Id}")).TotalCount >= 3);
    }

    private static async Task<PagedResult<AuditRecord>> AuditAsync(HttpClient http, string query) =>
        (await http.GetFromJsonAsync<PagedResult<AuditRecord>>($"/api/v1/audit-entries?pageSize=100&{query}"))!;

    private static async Task<ClientResponse> CreateClientAsync(HttpClient coach, string name)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput(name + " " + Guid.NewGuid().ToString("N")[..6], null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }
}
