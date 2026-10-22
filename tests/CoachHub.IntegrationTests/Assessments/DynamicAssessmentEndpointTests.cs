using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Clients;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using CoachHub.IntegrationTests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Assessments;

public sealed class DynamicAssessmentEndpointTests : IClassFixture<CoachHubApiFactory>
{
    private readonly CoachHubApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public DynamicAssessmentEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
        _admin = factory.CreateAdminClient();
    }

    [Fact]
    public async Task Published_dynamic_forms_support_all_types_history_and_one_initial_many_updates()
    {
        await AuthenticateAsync();
        var client = await CreateClientAsync();
        var initial = await CreateFormAsync("Initial " + Guid.NewGuid(), AssessmentFormType.InitialAssessment);
        var section = await PostAsync<SectionResponse>(
            $"/api/v1/assessment-forms/{initial.DefinitionId}/sections",
            new SectionInput("Profile", 0));
        var questions = new List<QuestionResponse>();
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Short", QuestionType.ShortText, true, 0));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Long", QuestionType.LongText, true, 1));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Number", QuestionType.Number, true, 2));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Date", QuestionType.Date, true, 3));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Boolean", QuestionType.Boolean, true, 4));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Single", QuestionType.SingleChoice, true, 5,
            [new("one", "One", 0), new("two", "Two", 1)]));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Multiple", QuestionType.MultipleChoice, true, 6,
            [new("a", "A", 0), new("b", "B", 1)]));
        questions.Add(await AddQuestionAsync(initial.DefinitionId, section.Id, "Media", QuestionType.MediaUpload, true, 7));
        var published = await PostEmptyAsync<FormVersionResponse>(
            $"/api/v1/assessment-forms/{initial.DefinitionId}/publish");
        Assert.Equal(FormVersionStatus.Published, published.Status);

        // D5: the client fills the form signed in; no codes.
        var portal = _factory.CreateClientAreaClient(client.CoachId, client.Id);
        var access = await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms");
        Assert.Contains(access!.EligibleForms, form => form.Id == initial.DefinitionId);
        var clientForm = await portal.GetFromJsonAsync<FormVersionResponse>($"/api/v1/me/forms/{initial.DefinitionId}");
        Assert.Equal(8, clientForm!.Questions.Count);
        var media = await UploadMediaAsync(portal);
        var answers = new[]
        {
            Answer(questions[0].Id, "text"),
            Answer(questions[1].Id, "long notes"),
            Answer(questions[2].Id, 82.5m),
            Answer(questions[3].Id, "1990-01-02"),
            Answer(questions[4].Id, true),
            Answer(questions[5].Id, "one"),
            Answer(questions[6].Id, new[] { "a", "b" }),
            new AnswerInput(questions[7].Id, JsonSerializer.SerializeToElement<object?>(null), media.Id)
        };
        var submittedResponse = await portal.PostAsJsonAsync(
            $"/api/v1/me/forms/{initial.DefinitionId}/submissions", new MyFormSubmissionInput(answers));
        Assert.Equal(HttpStatusCode.Created, submittedResponse.StatusCode);
        var submitted = (await submittedResponse.Content.ReadFromJsonAsync<SubmissionResponse>())!;
        Assert.NotEqual(Guid.Empty, submitted.SubmissionId);

        var duplicate = await portal.PostAsJsonAsync(
            $"/api/v1/me/forms/{initial.DefinitionId}/submissions", new MyFormSubmissionInput(answers));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        access = await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms");
        Assert.DoesNotContain(access!.EligibleForms, form => form.Id == initial.DefinitionId);

        await AuthenticateAsync();
        var draft = await PostEmptyAsync<FormVersionResponse>(
            $"/api/v1/assessment-forms/{initial.DefinitionId}/drafts");
        Assert.Equal(2, draft.VersionNumber);
        Assert.Equal(questions[0].StableKey, draft.Questions[0].StableKey);
        var changed = draft.Questions[0];
        await PutAsync<QuestionResponse>(
            $"/api/v1/assessment-forms/{initial.DefinitionId}/questions/{changed.Id}",
            new QuestionInput(changed.SectionId, "Changed text", changed.QuestionType, true, changed.Order, []));
        await PostEmptyAsync<FormVersionResponse>($"/api/v1/assessment-forms/{initial.DefinitionId}/publish");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
            var stored = await db.Set<FormAnswer>().SingleAsync(answer =>
                answer.FormSubmissionId == submitted.SubmissionId &&
                answer.QuestionStableKey == questions[0].StableKey);
            Assert.Equal("Short", stored.QuestionTextSnapshot);
            Assert.Equal(SubmissionSource.CoachHubSystem,
                (await db.Set<FormSubmission>().SingleAsync(item => item.Id == submitted.SubmissionId)).Source);
        }

        var update = await CreateFormAsync("Update " + Guid.NewGuid(), AssessmentFormType.UpdateAssessment);
        var updateQuestion = await AddQuestionAsync(
            update.DefinitionId, null, "Progress okay?", QuestionType.Boolean, true, 0);
        await PostEmptyAsync<FormVersionResponse>($"/api/v1/assessment-forms/{update.DefinitionId}/publish");
        var updateInput = new MyFormSubmissionInput([Answer(updateQuestion.Id, true)]);
        (await portal.PostAsJsonAsync($"/api/v1/me/forms/{update.DefinitionId}/submissions", updateInput)).EnsureSuccessStatusCode();
        (await portal.PostAsJsonAsync($"/api/v1/me/forms/{update.DefinitionId}/submissions", updateInput)).EnsureSuccessStatusCode();

        await AuthenticateAsync();
        var detail = await _client.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{client.Id}");
        // Only a check-in waiting for review makes a client "needs review" (P18A removed the plan workflow status).
        Assert.Equal(ClientStatus.OnTrack, detail!.Client.Status);

        var forms = await _client.GetFromJsonAsync<CoachHub.Application.Common.Models.PagedResult<FormSummary>>(
            $"/api/v1/assessment-forms?searchTerm={Uri.EscapeDataString(initial.Name)}");
        Assert.Contains(forms!.Items, item => item.Id == initial.DefinitionId);
        var submissions = await _client.GetFromJsonAsync<CoachHub.Application.Common.Models.PagedResult<AssessmentSubmissionSummary>>(
            $"/api/v1/assessment-submissions?clientId={client.Id}");
        Assert.Contains(submissions!.Items, item => item.Id == submitted.SubmissionId && item.AnswerCount == 8);
        var reviewed = await _client.GetFromJsonAsync<AssessmentSubmissionDetail>(
            $"/api/v1/assessment-submissions/{submitted.SubmissionId}");
        Assert.Equal(8, reviewed!.Answers.Count);
        Assert.Contains(reviewed.Answers, answer => answer.QuestionText == "Short" && answer.ValueJson.Contains("text"));

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _admin.DeleteAsync($"/api/v1/media/{media.Id}")).StatusCode);
    }

    [Fact]
    public async Task Forms_need_a_signed_in_client_and_the_retired_code_routes_are_gone()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/me/forms")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/api/v1/client-forms/access/validate",
            new { clientCode = "BADCODE1", formCode = "BADFORM001" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.GetAsync("/api/v1/me/forms")).StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_open_or_submit_another_coachs_form()
    {
        await AuthenticateAsync();
        var form = await CreateFormAsync("Private " + Guid.NewGuid(), AssessmentFormType.UpdateAssessment);
        var question = await AddQuestionAsync(form.DefinitionId, null, "Okay?", QuestionType.Boolean, true, 0);
        await PostEmptyAsync<FormVersionResponse>($"/api/v1/assessment-forms/{form.DefinitionId}/publish");
        var otherCoach = await _factory.CreateCoachClientAsync();
        var otherClient = await (await otherCoach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Other coach client", null, null, null))).Content.ReadFromJsonAsync<ClientResponse>();
        var portal = _factory.CreateClientAreaClient(otherClient!.CoachId, otherClient.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync($"/api/v1/me/forms/{form.DefinitionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.PostAsJsonAsync($"/api/v1/me/forms/{form.DefinitionId}/submissions",
            new MyFormSubmissionInput([Answer(question.Id, true)]))).StatusCode);
    }

    private System.Net.Http.Headers.AuthenticationHeaderValue? _coachAuthorization;

    /// <summary>
    /// Signs in as this test's coach (created on first use): tenant data is created in that
    /// coach's workspace, isolated from every other test.
    /// </summary>
    private async Task AuthenticateAsync()
    {
        _coachAuthorization ??= (await _factory.CreateCoachClientAsync()).DefaultRequestHeaders.Authorization;
        _client.DefaultRequestHeaders.Authorization = _coachAuthorization;
    }

    private async Task<ClientResponse> CreateClientAsync() => await PostAsync<ClientResponse>(
        "/api/v1/clients", new ClientCreateInput("Assessment Client " + Guid.NewGuid(), null, null, null));
    private async Task<FormVersionResponse> CreateFormAsync(string name, AssessmentFormType type) =>
        await PostAsync<FormVersionResponse>("/api/v1/assessment-forms", new CreateFormInput(name, type));
    private async Task<QuestionResponse> AddQuestionAsync(
        Guid definitionId, Guid? sectionId, string text, QuestionType type, bool required,
        int order, IReadOnlyList<OptionInput>? options = null) => await PostAsync<QuestionResponse>(
            $"/api/v1/assessment-forms/{definitionId}/questions",
            new QuestionInput(sectionId, text, type, required, order, options ?? []));
    private static AnswerInput Answer<T>(Guid questionId, T value) =>
        new(questionId, JsonSerializer.SerializeToElement(value), null);

    private static async Task<CoachHub.Application.Media.MediaMetadata> UploadMediaAsync(HttpClient portal)
    {
        using var form = new MultipartFormDataContent();
        using var bytes = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "assessment.png");
        var response = await portal.PostAsync("/api/v1/me/forms/media", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CoachHub.Application.Media.MediaMetadata>())!;
    }
    private async Task<T> PostAsync<T>(string url, object input)
    {
        var response = await _client.PostAsJsonAsync(url, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<T> PostEmptyAsync<T>(string url)
    {
        var response = await _client.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private async Task<T> PutAsync<T>(string url, object input)
    {
        var response = await _client.PutAsJsonAsync(url, input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}