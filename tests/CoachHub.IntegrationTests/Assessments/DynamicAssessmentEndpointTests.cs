using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth.Login;
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

    public DynamicAssessmentEndpointTests(CoachHubApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Published_dynamic_forms_support_all_types_history_and_one_initial_many_updates()
    {
        await AuthenticateAsync();
        var client = await CreateClientAsync();
        var initial = await CreateFormAsync("Initial " + Guid.NewGuid(), AssessmentFormType.InitialAssessment);
        var section = await PostAsync<SectionResponse>(
            $"/api/assessment-forms/{initial.DefinitionId}/sections",
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
            $"/api/assessment-forms/{initial.DefinitionId}/publish");
        Assert.Equal(FormVersionStatus.Published, published.Status);

        _client.DefaultRequestHeaders.Authorization = null;
        var accessInput = new FormAccessInput(client.ClientCode, client.FormCode);
        var access = await PostAsync<FormAccessResponse>("/api/client-forms/access/validate", accessInput);
        Assert.Contains(access.EligibleForms, form => form.Id == initial.DefinitionId);
        var clientForm = await PostAsync<FormVersionResponse>(
            $"/api/client-forms/{initial.DefinitionId}/questions", accessInput);
        Assert.Equal(8, clientForm.Questions.Count);
        var media = await UploadMediaAsync(client.ClientCode, client.FormCode);
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
        var submitted = await PostAsync<SubmissionResponse>(
            "/api/client-forms/submissions",
            new SubmitFormInput(client.ClientCode, client.FormCode, initial.DefinitionId, answers));
        Assert.NotEqual(Guid.Empty, submitted.SubmissionId);

        var duplicate = await _client.PostAsJsonAsync(
            "/api/client-forms/submissions",
            new SubmitFormInput(client.ClientCode, client.FormCode, initial.DefinitionId, answers));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        access = await PostAsync<FormAccessResponse>("/api/client-forms/access/validate", accessInput);
        Assert.DoesNotContain(access.EligibleForms, form => form.Id == initial.DefinitionId);

        await AuthenticateAsync();
        var draft = await PostEmptyAsync<FormVersionResponse>(
            $"/api/assessment-forms/{initial.DefinitionId}/drafts");
        Assert.Equal(2, draft.VersionNumber);
        Assert.Equal(questions[0].StableKey, draft.Questions[0].StableKey);
        var changed = draft.Questions[0];
        await PutAsync<QuestionResponse>(
            $"/api/assessment-forms/{initial.DefinitionId}/questions/{changed.Id}",
            new QuestionInput(changed.SectionId, "Changed text", changed.QuestionType, true, changed.Order, []));
        await PostEmptyAsync<FormVersionResponse>($"/api/assessment-forms/{initial.DefinitionId}/publish");

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
        await PostEmptyAsync<FormVersionResponse>($"/api/assessment-forms/{update.DefinitionId}/publish");
        _client.DefaultRequestHeaders.Authorization = null;
        var updateInput = new SubmitFormInput(
            client.ClientCode, client.FormCode, update.DefinitionId,
            [Answer(updateQuestion.Id, true)]);
        (await _client.PostAsJsonAsync("/api/client-forms/submissions", updateInput)).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/api/client-forms/submissions", updateInput)).EnsureSuccessStatusCode();

        await AuthenticateAsync();
        var detail = await _client.GetFromJsonAsync<ClientDetailResponse>($"/api/clients/{client.Id}");
        Assert.Equal(PlanWorkflowStatus.ReviewRequired, detail!.Client.DietStatus);
        Assert.Equal(PlanWorkflowStatus.ReviewRequired, detail.Client.WorkoutStatus);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await _client.DeleteAsync($"/api/media/{media.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_codes_do_not_expose_forms()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/client-forms/access/validate",
            new FormAccessInput("BADCODE1", "BADFORM001"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("BADFORM001", await response.Content.ReadAsStringAsync());
    }

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = CoachHubApiFactory.AdminEmail, password = CoachHubApiFactory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<ClientResponse> CreateClientAsync() => await PostAsync<ClientResponse>(
        "/api/clients", new ClientCreateInput("Assessment Client " + Guid.NewGuid(), null, null, null));
    private async Task<FormVersionResponse> CreateFormAsync(string name, AssessmentFormType type) =>
        await PostAsync<FormVersionResponse>("/api/assessment-forms", new CreateFormInput(name, type));
    private async Task<QuestionResponse> AddQuestionAsync(
        Guid definitionId, Guid? sectionId, string text, QuestionType type, bool required,
        int order, IReadOnlyList<OptionInput>? options = null) => await PostAsync<QuestionResponse>(
            $"/api/assessment-forms/{definitionId}/questions",
            new QuestionInput(sectionId, text, type, required, order, options ?? []));
    private static AnswerInput Answer<T>(Guid questionId, T value) =>
        new(questionId, JsonSerializer.SerializeToElement(value), null);

    private async Task<CoachHub.Application.Media.MediaMetadata> UploadMediaAsync(
        string clientCode, string formCode)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(clientCode), "clientCode");
        form.Add(new StringContent(formCode), "formCode");
        using var bytes = new ByteArrayContent([1, 2, 3]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "assessment.png");
        var response = await _client.PostAsync("/api/client-forms/media", form);
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