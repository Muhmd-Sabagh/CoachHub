using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Assessments;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// Regression (found in P7): the submission queries joined form definitions with IgnoreQueryFilters,
/// which lifts tenant filters for the whole query, so a coach could list every coach's submissions.
/// </summary>
public sealed class AssessmentSubmissionIsolationTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Coaches_see_only_their_clients_submissions_and_transfers_keep_form_names()
    {
        var coachAId = await factory.CreateCoachAsync();
        var coachBId = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachAId);
        var coachB = factory.CreateCoachClient(coachBId);
        var formName = "Weekly " + Guid.NewGuid().ToString("N")[..6];
        var form = await ReadAsync<FormVersionResponse>(await coachA.PostAsJsonAsync("/api/v1/assessment-forms",
            new CreateFormInput(formName, AssessmentFormType.UpdateAssessment)));
        var question = await ReadAsync<QuestionResponse>(await coachA.PostAsJsonAsync(
            $"/api/v1/assessment-forms/{form.DefinitionId}/questions",
            new QuestionInput(null, "How was your week?", QuestionType.ShortText, true, 0, [])));
        (await coachA.PostAsync($"/api/v1/assessment-forms/{form.DefinitionId}/publish", null)).EnsureSuccessStatusCode();
        var client = await ReadAsync<ClientResponse>(await coachA.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Isolated client", null, null, null)));
        var portal = factory.CreateClientAreaClient(coachAId, client.Id);
        (await portal.PostAsJsonAsync($"/api/v1/me/forms/{form.DefinitionId}/submissions", new MyFormSubmissionInput(
            [new AnswerInput(question.Id, JsonSerializer.SerializeToElement("Good"), null)]))).EnsureSuccessStatusCode();

        var mine = await coachA.GetFromJsonAsync<PagedResult<AssessmentSubmissionSummary>>("/api/v1/assessment-submissions");
        var submission = Assert.Single(mine!.Items);
        Assert.Equal(formName, submission.FormName);

        var others = await coachB.GetFromJsonAsync<PagedResult<AssessmentSubmissionSummary>>("/api/v1/assessment-submissions");
        Assert.Equal(0, others!.TotalCount);
        var searched = await coachB.GetFromJsonAsync<PagedResult<AssessmentSubmissionSummary>>(
            $"/api/v1/assessment-submissions?searchTerm={Uri.EscapeDataString(formName)}");
        Assert.Equal(0, searched!.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/assessment-submissions/{submission.Id}")).StatusCode);

        // After a transfer the new coach sees the submission, still named after the previous coach's form.
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachBId))).EnsureSuccessStatusCode();
        var moved = await coachB.GetFromJsonAsync<PagedResult<AssessmentSubmissionSummary>>(
            $"/api/v1/assessment-submissions?searchTerm={Uri.EscapeDataString(formName)}");
        Assert.Equal(formName, Assert.Single(moved!.Items).FormName);
        Assert.Equal(HttpStatusCode.OK, (await coachB.GetAsync($"/api/v1/assessment-submissions/{submission.Id}")).StatusCode);
        Assert.Equal(0, (await coachA.GetFromJsonAsync<PagedResult<AssessmentSubmissionSummary>>("/api/v1/assessment-submissions"))!.TotalCount);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
