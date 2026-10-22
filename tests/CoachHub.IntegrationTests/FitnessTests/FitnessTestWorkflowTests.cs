using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.FitnessTests;
using CoachHub.Domain.FitnessTests;
using CoachHub.Infrastructure.FitnessTests;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.FitnessTests;

/// <summary>
/// P13 on SQL Server: the test library (§34–§36) with platform and custom tests, assignments, results recorded by the
/// client and the coach, cut-off classification with the coach's override, progress, isolation (§62) and transfer (D7).
/// </summary>
public sealed class FitnessTestWorkflowTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly Guid PlankId = TestDefinitionSeed.All.Single(x => x.NameEn == "Plank hold").Id;

    [Fact]
    public async Task The_library_holds_platform_and_custom_tests()
    {
        var setup = await SetUpAsync();
        using var admin = factory.CreateAdminClient();

        // The starter library is there for every coach.
        var platform = (await setup.Coach.GetFromJsonAsync<PagedResult<TestDefinitionResponse>>("/api/v1/fitness-tests?scope=Platform&pageSize=100"))!;
        Assert.True(platform.Items.Count >= TestDefinitionSeed.All.Length);
        Assert.Contains(platform.Items, x => x.Id == PlankId && x.Kind == TestKind.Endurance && x.Unit == TestUnit.Seconds && x.HigherIsBetter);

        // Coaches cannot change platform tests; administrators can.
        var plank = platform.Items.Single(x => x.Id == PlankId);
        var input = Input(plank) with { GoodThreshold = 120, PoorThreshold = 45 };
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.PutAsJsonAsync($"/api/v1/fitness-tests/{PlankId}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.DeleteAsync($"/api/v1/fitness-tests/{PlankId}")).StatusCode);
        var created = await ReadAsync<TestDefinitionResponse>(await admin.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Mobility, "Platform reach " + setup.Tag, null, TestUnit.Centimeters, true)));
        Assert.Equal((null, false), (created.CoachId, created.IsCustom));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/fitness-tests/{created.Id}")).StatusCode);

        // Custom tests: validated, visible to their coach only, deletable while unused.
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Strength, "Bad " + setup.Tag, null, TestUnit.Kilograms, true, 100, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Strength, "Bad " + setup.Tag, null, TestUnit.Kilograms, true, 50, 100))).StatusCode);
        var custom = await ReadAsync<TestDefinitionResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Mobility, "Hip rotation " + setup.Tag, "دوران الورك", TestUnit.Degrees, true, 40, 25,
                "Lie face down and let the shin fall out.", null, "https://www.youtube.com/watch?v=abc")));
        Assert.Equal((setup.CoachId, true), (custom.CoachId!.Value, custom.IsCustom));
        var mine = (await setup.Coach.GetFromJsonAsync<PagedResult<TestDefinitionResponse>>($"/api/v1/fitness-tests?scope=Custom&kind=Mobility&searchTerm={setup.Tag}"))!;
        Assert.Equal(custom.Id, Assert.Single(mine.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);
        Assert.DoesNotContain((await setup.OtherCoach.GetFromJsonAsync<PagedResult<TestDefinitionResponse>>($"/api/v1/fitness-tests?searchTerm={setup.Tag}"))!.Items,
            x => x.Id == custom.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.DeleteAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Portal.GetAsync("/api/v1/fitness-tests")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.DeleteAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);
    }

    [Fact]
    public async Task Clients_record_assigned_tests_and_coaches_classify_and_correct()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        var custom = await ReadAsync<TestDefinitionResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Strength, "Push-ups " + setup.Tag, null, TestUnit.Repetitions, true, 30, 15)));
        var results = $"/api/v1/clients/{setup.ClientId}/fitness-tests/results";

        // Nothing assigned: the client sees no tests and cannot record one.
        Assert.Empty((await portal.GetFromJsonAsync<ClientTestsResponse>("/api/v1/me/fitness-tests"))!.Tests);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today, 20))).StatusCode);

        // The coach assigns a custom and a platform test (twice changes nothing); another coach's test is refused.
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.PutAsync($"{AssignPath(setup)}/{custom.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.PutAsync($"{AssignPath(setup)}/{custom.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.PutAsync($"{AssignPath(setup)}/{PlankId}", null)).StatusCode);
        var foreign = await ReadAsync<TestDefinitionResponse>(await setup.OtherCoach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Strength, "Foreign " + setup.Tag, null, TestUnit.Kilograms, true)));
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.PutAsync($"{AssignPath(setup)}/{foreign.Id}", null)).StatusCode);

        // The client records assigned tests; a bad value or a future date fails (D3).
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today, -3))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today.AddDays(5), 20))).StatusCode);
        var first = await ReadAsync<TestResultResponse>(await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today.AddDays(-28), 12, "Struggled")));
        Assert.Equal((TestClassification.Poor, TestRecorder.Client, TestUnit.Repetitions), (first.Classification, first.RecordedBy, first.Unit));
        var second = await ReadAsync<TestResultResponse>(await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today.AddDays(-14), 20)));
        Assert.Equal(TestClassification.NeedsImprovement, second.Classification);
        await ReadAsync<TestResultResponse>(await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results", new TestResultInput(PlankId, Today, 75)));

        // The coach records a result after an in-person session; the client cannot change it, the coach can.
        var coached = await ReadAsync<TestResultResponse>(await setup.Coach.PostAsJsonAsync(results,
            new TestResultInput(custom.Id, Today, 31, "Strict form")));
        Assert.Equal((TestClassification.Good, TestRecorder.Coach), (coached.Classification, coached.RecordedBy));
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PutAsJsonAsync($"/api/v1/me/fitness-tests/results/{coached.Id}",
            new TestResultChangeInput(Today, 40))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.DeleteAsync($"/api/v1/me/fitness-tests/results/{coached.Id}")).StatusCode);
        coached = await ReadAsync<TestResultResponse>(await setup.Coach.PutAsJsonAsync($"{results}/{coached.Id}",
            new TestResultChangeInput(Today, 29, "Strict form, one rep missed")));
        Assert.Equal(TestClassification.NeedsImprovement, coached.Classification);

        // The coach overrides the classification and can return to the cut-offs.
        coached = await ReadAsync<TestResultResponse>(await setup.Coach.PutAsJsonAsync($"{results}/{coached.Id}/classification",
            new TestClassificationInput(TestClassification.Good)));
        Assert.Equal((TestClassification.Good, true), (coached.Classification, coached.IsClassificationOverridden));
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PutAsJsonAsync($"/api/v1/clients/{setup.ClientId}/fitness-tests/results/{coached.Id}/classification",
            new TestClassificationInput(null))).StatusCode);

        // The client changes their own result.
        var changed = await ReadAsync<TestResultResponse>(await portal.PutAsJsonAsync($"/api/v1/me/fitness-tests/results/{second.Id}",
            new TestResultChangeInput(Today.AddDays(-14), 21, "Recounted")));
        Assert.Equal((21m, "Recounted"), (changed.Value, changed.Notes));

        // Previous vs current and progress (§35), the same for both sides.
        var overview = (await setup.Coach.GetFromJsonAsync<ClientTestsResponse>($"/api/v1/clients/{setup.ClientId}/fitness-tests"))!;
        var pushUps = overview.Tests.Single(x => x.TestDefinitionId == custom.Id);
        Assert.True(pushUps.IsAssigned);
        Assert.Equal((3, 12m, 21m, 29m, 8m, 17m, 141.7m, TestTrend.Improved), (pushUps.Progress.Count, pushUps.Progress.First!.Value,
            pushUps.Progress.Previous!.Value, pushUps.Progress.Current!.Value, pushUps.Progress.ChangeFromPrevious!.Value,
            pushUps.Progress.ChangeFromFirst!.Value, pushUps.Progress.PercentChangeFromFirst!.Value, pushUps.Progress.Trend!.Value));
        Assert.Equal(coached.Id, pushUps.Results[0].Id);
        Assert.Equal(new[] { TestKind.Strength, TestKind.Endurance }, overview.Tests.Select(x => x.Kind));
        var own = (await portal.GetFromJsonAsync<ClientTestsResponse>("/api/v1/me/fitness-tests"))!;
        Assert.Equal(pushUps.Progress, own.Tests.Single(x => x.TestDefinitionId == custom.Id).Progress);
        Assert.Equal("Push-ups " + setup.Tag, own.Tests.Single(x => x.TestDefinitionId == custom.Id).Definition!.NameEn);

        // In use: the test cannot be deleted, only deactivated; inactive tests take no new results.
        Assert.Equal(HttpStatusCode.Conflict, (await setup.Coach.DeleteAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);
        await ReadAsync<TestDefinitionResponse>(await setup.Coach.PutAsJsonAsync($"/api/v1/fitness-tests/{custom.Id}", Input(custom) with { IsActive = false }));
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today, 25))).StatusCode);

        // Unassigning keeps the history; the client can no longer record the test.
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.DeleteAsync($"{AssignPath(setup)}/{PlankId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.DeleteAsync($"{AssignPath(setup)}/{PlankId}")).StatusCode);
        own = (await portal.GetFromJsonAsync<ClientTestsResponse>("/api/v1/me/fitness-tests"))!;
        var plank = own.Tests.Single(x => x.TestDefinitionId == PlankId);
        Assert.Equal((false, 1), (plank.IsAssigned, plank.Results.Count));
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(PlankId, Today, 80))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"/api/v1/me/fitness-tests/results/{first.Id}")).StatusCode);
    }

    [Fact]
    public async Task Tests_are_isolated_and_follow_the_client_on_transfer()
    {
        var setup = await SetUpAsync();
        var custom = await ReadAsync<TestDefinitionResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/fitness-tests",
            new TestDefinitionInput(TestKind.Mobility, "Reach " + setup.Tag, null, TestUnit.Centimeters, true)));
        await setup.Coach.PutAsync($"{AssignPath(setup)}/{custom.Id}", null);
        await setup.Coach.PutAsync($"{AssignPath(setup)}/{PlankId}", null);
        var result = await ReadAsync<TestResultResponse>(await setup.Portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results",
            new TestResultInput(custom.Id, Today, 12)));
        await ReadAsync<TestResultResponse>(await setup.Portal.PostAsJsonAsync("/api/v1/me/fitness-tests/results", new TestResultInput(PlankId, Today, 60)));

        // Another coach and a sibling client find nothing; roles stay in their lanes.
        var path = $"/api/v1/clients/{setup.ClientId}/fitness-tests";
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.PutAsJsonAsync($"{path}/results/{result.Id}", new TestResultChangeInput(Today, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.PutAsync($"{path}/assignments/{PlankId}", null)).StatusCode);
        var sibling = await ReadAsync<ClientResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Sibling " + setup.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientAreaClient(setup.CoachId, sibling.Id);
        Assert.Empty((await siblingPortal.GetFromJsonAsync<ClientTestsResponse>("/api/v1/me/fitness-tests"))!.Tests);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.DeleteAsync($"/api/v1/me/fitness-tests/results/{result.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Portal.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.GetAsync("/api/v1/me/fitness-tests")).StatusCode);

        // Transfer (D7): results follow the client and stay readable; the old coach's custom test assignment ends.
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{setup.ClientId}/transfer", new ClientTransferInput(setup.OtherCoachId))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync(path)).StatusCode);
        var moved = (await setup.OtherCoach.GetFromJsonAsync<ClientTestsResponse>(path))!;
        var reach = moved.Tests.Single(x => x.TestDefinitionId == custom.Id);
        Assert.Equal(("Reach " + setup.Tag, false, 12m), (reach.NameEn, reach.IsAssigned, reach.Progress.Current!.Value));
        Assert.Null(reach.Definition);
        Assert.True(moved.Tests.Single(x => x.TestDefinitionId == PlankId).IsAssigned);
        // The previous coach's test is still in use, so it cannot be deleted.
        Assert.Equal(HttpStatusCode.Conflict, (await setup.Coach.DeleteAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);

        // Deleting the client removes its tests' results and assignments.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{setup.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await setup.Coach.DeleteAsync($"/api/v1/fitness-tests/{custom.Id}")).StatusCode);
    }

    private static string AssignPath(Setup setup) => $"/api/v1/clients/{setup.ClientId}/fitness-tests/assignments";

    private static TestDefinitionInput Input(TestDefinitionResponse x) => new(x.Kind, x.NameEn, x.NameAr, x.Unit, x.HigherIsBetter,
        x.GoodThreshold, x.PoorThreshold, x.InstructionsEn, x.InstructionsAr, x.YouTubeUrl, x.IsActive);

    private sealed record Setup(Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, HttpClient Portal, string Tag);

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoachId = await factory.CreateCoachAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Tester " + tag, null, null, null)));
        var portal = (await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id)).Http;
        return new Setup(coachId, coach, otherCoachId, factory.CreateCoachClient(otherCoachId), client.Id, portal, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
