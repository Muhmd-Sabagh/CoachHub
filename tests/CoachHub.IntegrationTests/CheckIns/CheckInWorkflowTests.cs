using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Adherence;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.Progress;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Progress;
using CoachHub.Infrastructure.Persistence;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.CheckIns;

/// <summary>
/// P12 on SQL Server: check-in forms with metric-bound questions (§28), schedules and due dates, the coach's review
/// (§29), adherence (§30) and the computed client status (§39), with tenant isolation (§62) and transfer (D7).
/// </summary>
public sealed class CheckInWorkflowTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_check_in_records_metrics_and_goes_through_the_review()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        var formsPath = $"/api/v1/me/forms/{setup.Form.DefinitionId}";

        // Not scheduled yet: the client is not offered the check-in and cannot submit it.
        Assert.DoesNotContain((await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms"))!.EligibleForms, x => x.Id == setup.Form.DefinitionId);
        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsJsonAsync($"{formsPath}/submissions", Answers(setup, 81.5m, null))).StatusCode);

        // Only an active, published check-in form of the coach can be scheduled.
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PutAsJsonAsync(SchedulePath(setup),
            new CheckInScheduleInput(setup.InitialForm.DefinitionId, 1, Today.AddDays(2).DayOfWeek))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PutAsJsonAsync(SchedulePath(setup),
            new CheckInScheduleInput(setup.Form.DefinitionId, 3, Today.AddDays(2).DayOfWeek))).StatusCode);
        var schedule = await ReadAsync<CheckInScheduleResponse>(await setup.Coach.PutAsJsonAsync(SchedulePath(setup),
            new CheckInScheduleInput(setup.Form.DefinitionId, 1, Today.AddDays(2).DayOfWeek, Today)));
        Assert.Equal((Today.AddDays(2), CheckInDueState.Pending, Today.AddDays(-1)), (schedule.NextDueOn!.Value, schedule.State!.Value, schedule.OpensOn!.Value));

        // Open: offered, and a bad measure fails before anything is stored (D3).
        Assert.Contains((await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms"))!.EligibleForms, x => x.Id == setup.Form.DefinitionId);
        var form = (await portal.GetFromJsonAsync<FormVersionResponse>(formsPath))!;
        Assert.Equal(QuestionMetric.WeightKg, form.Questions.Single(x => x.Id == setup.WeightQuestion).Metric);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync($"{formsPath}/submissions", Answers(setup, 5m, null))).StatusCode);
        Assert.Empty((await portal.GetFromJsonAsync<MyCheckInsResponse>("/api/v1/me/check-ins"))!.History);

        var photo = await UploadAsync(portal);
        (await portal.PostAsJsonAsync($"{formsPath}/submissions", Answers(setup, 81.5m, photo.Id))).EnsureSuccessStatusCode();
        var mine = (await portal.GetFromJsonAsync<MyCheckInsResponse>("/api/v1/me/check-ins"))!;
        var checkIn = Assert.Single(mine.History);
        Assert.Equal((Today.AddDays(2), false, CheckInStatus.AwaitingReview), (checkIn.DueOn, checkIn.IsLate, checkIn.Status));
        Assert.Equal(Today.AddDays(9), mine.Schedule!.NextDueOn);
        Assert.Equal(CheckInDueState.Upcoming, mine.Schedule.State);
        Assert.DoesNotContain((await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms"))!.EligibleForms, x => x.Id == setup.Form.DefinitionId);
        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsJsonAsync($"{formsPath}/submissions", Answers(setup, 81m, null))).StatusCode);

        // The bound answers became typed entries: a weigh-in and a front progress photo.
        var body = (await portal.GetFromJsonAsync<BodyProgressResponse>($"/api/v1/me/progress/body?from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))!;
        Assert.Equal(81.5m, body.Weight.Summary.Current!.Value);
        var progressPhoto = Assert.Single((await portal.GetFromJsonAsync<ProgressPhotoResponse[]>("/api/v1/me/progress/photos"))!);
        Assert.Equal((PhotoPose.Front, photo.Id), (progressPhoto.Pose, progressPhoto.MediaId));

        // The coach sees it waiting; the client needs review (§39).
        Assert.Equal(ClientStatus.NeedsReview, (await ClientAsync(setup)).Status);
        var waiting = (await setup.Coach.GetFromJsonAsync<PagedResult<CheckInSummary>>($"/api/v1/check-ins?status=AwaitingReview&clientId={setup.ClientId}"))!;
        Assert.Equal(checkIn.Id, Assert.Single(waiting.Items).Id);
        var needsReview = (await setup.Coach.GetFromJsonAsync<PagedResult<ClientResponse>>("/api/v1/clients?status=NeedsReview&pageSize=100"))!;
        Assert.Contains(needsReview.Items, x => x.Id == setup.ClientId);

        // Review: private notes, request changes (a message is required), resubmit, approve with feedback.
        var path = $"/api/v1/check-ins/{checkIn.Id}";
        var detail = await ReadAsync<CheckInDetail>(await setup.Coach.PutAsJsonAsync($"{path}/notes", new CheckInNotesInput("Private: watch the knee")));
        Assert.Equal((3, "Private: watch the knee"), (detail.Answers.Count, detail.PrivateNotes));
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PostAsJsonAsync($"{path}/request-changes", new CheckInReviewInput(null))).StatusCode);
        detail = await ReadAsync<CheckInDetail>(await setup.Coach.PostAsJsonAsync($"{path}/request-changes", new CheckInReviewInput("Please add comments.")));
        Assert.Equal(CheckInStatus.ChangesRequested, detail.CheckIn.Status);
        Assert.Equal(ClientStatus.OnTrack, (await ClientAsync(setup)).Status);

        var raw = await portal.GetStringAsync("/api/v1/me/check-ins");
        Assert.DoesNotContain("watch the knee", raw);
        mine = JsonSerializer.Deserialize<MyCheckInsResponse>(raw, Json)!;
        Assert.Equal(checkIn.Id, mine.ResubmitCheckInId);
        Assert.Equal("Please add comments.", Assert.Single(mine.History[0].Replies).Text);
        Assert.Contains((await portal.GetFromJsonAsync<MyFormsResponse>("/api/v1/me/forms"))!.EligibleForms, x => x.Id == setup.Form.DefinitionId);
        (await portal.PostAsJsonAsync($"{formsPath}/submissions", Answers(setup, 81.4m, null, "Feeling good"))).EnsureSuccessStatusCode();
        var resubmitted = await portal.GetFromJsonAsync<MyCheckInResponse>($"/api/v1/me/check-ins/{checkIn.Id}");
        Assert.Equal((CheckInStatus.AwaitingReview, 2), (resubmitted!.Status, resubmitted.Submissions.Count));
        Assert.Contains(resubmitted.Answers, x => x.ValueJson.Contains("Feeling good"));

        detail = await ReadAsync<CheckInDetail>(await setup.Coach.PostAsJsonAsync($"{path}/approve", new CheckInReviewInput("Great week!")));
        Assert.Equal((CheckInStatus.Approved, 2, 2), (detail.CheckIn.Status, detail.Replies.Count, detail.Submissions.Count));
        Assert.Equal(ClientStatus.OnTrack, (await ClientAsync(setup)).Status);
        detail = await ReadAsync<CheckInDetail>(await setup.Coach.PostAsJsonAsync($"{path}/replies", new CheckInReplyInput("Keep it up.")));
        Assert.Equal(3, detail.Replies.Count);

        // The dashboard (§37) shows the latest check-in and the coach's latest feedback.
        var dashboard = (await portal.GetFromJsonAsync<ProgressDashboardResponse>(
            $"/api/v1/me/progress/dashboard?from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))!;
        Assert.Equal((checkIn.Id, "Keep it up."), (dashboard.CheckIn.Latest!.Id, dashboard.CheckIn.LatestFeedback!.Text));
    }

    [Fact]
    public async Task Overdue_check_ins_adherence_isolation_and_transfer()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        // Weekly, due yesterday's weekday, from two weeks ago: due 8 days ago and yesterday; the next due date is in 6 days.
        await ReadAsync<CheckInScheduleResponse>(await setup.Coach.PutAsJsonAsync(SchedulePath(setup),
            new CheckInScheduleInput(setup.Form.DefinitionId, 1, Today.AddDays(-1).DayOfWeek, Today.AddDays(-14))));
        // Time passes: the client missed the check-in due two days ago.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
            await db.Set<Client>().IgnoreQueryFilters().Where(x => x.Id == setup.ClientId)
                .ExecuteUpdateAsync(x => x.SetProperty(client => client.CheckInDueOn, Today.AddDays(-2)));
        }
        Assert.Equal(ClientStatus.CheckInOverdue, (await ClientAsync(setup)).Status);
        var overdue = (await setup.Coach.GetFromJsonAsync<PagedResult<CheckInDueResponse>>("/api/v1/check-ins/due?state=Overdue&pageSize=100"))!;
        Assert.Equal(2, Assert.Single(overdue.Items, x => x.ClientId == setup.ClientId).DaysOverdue);
        Assert.Contains((await setup.Coach.GetFromJsonAsync<PagedResult<ClientResponse>>("/api/v1/clients?status=CheckInOverdue&pageSize=100"))!.Items,
            x => x.Id == setup.ClientId);

        // A late check-in counts for the latest due date that passed (yesterday), marked late.
        (await portal.PostAsJsonAsync($"/api/v1/me/forms/{setup.Form.DefinitionId}/submissions", Answers(setup, 80m, null))).EnsureSuccessStatusCode();
        var checkIn = Assert.Single((await portal.GetFromJsonAsync<MyCheckInsResponse>("/api/v1/me/check-ins"))!.History);
        Assert.Equal((Today.AddDays(-1), true), (checkIn.DueOn, checkIn.IsLate));
        Assert.Equal(ClientStatus.NeedsReview, (await ClientAsync(setup)).Status);

        // Adherence (§30): one of two check-ins due; steps met on 1 of 14 days; weights 15 and 5.
        await setup.Coach.PutAsJsonAsync($"/api/v1/clients/{setup.ClientId}/progress/targets", new LifestyleTargetValues(10000, null, null));
        (await portal.PutAsJsonAsync($"/api/v1/me/progress/steps/{Today:yyyy-MM-dd}", new StepsInput(12000))).EnsureSuccessStatusCode();
        var range = $"from={Today.AddDays(-13):yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var adherence = (await setup.Coach.GetFromJsonAsync<AdherenceResponse>($"/api/v1/clients/{setup.ClientId}/progress/adherence?{range}"))!;
        Assert.Equal(50m, adherence.Areas.Single(x => x.Area == AdherenceArea.CheckIn).Percent);
        Assert.Equal(7.1m, adherence.Areas.Single(x => x.Area == AdherenceArea.Steps).Percent);
        Assert.Null(adherence.Areas.Single(x => x.Area == AdherenceArea.Water).Percent);
        Assert.Equal(39.3m, adherence.OverallPercent);
        Assert.Equal(adherence.OverallPercent, (await portal.GetFromJsonAsync<AdherenceResponse>($"/api/v1/me/progress/adherence?{range}"))!.OverallPercent);
        var dashboard = (await setup.Coach.GetFromJsonAsync<ProgressDashboardResponse>($"/api/v1/clients/{setup.ClientId}/progress/dashboard?{range}"))!;
        Assert.Equal((1, 2, 1), (dashboard.CheckIn.Adherence.Completed, dashboard.CheckIn.Adherence.Due, dashboard.CheckIn.Adherence.Late));

        // Isolation: another coach finds nothing; a sibling client neither; roles stay in their lanes.
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"/api/v1/check-ins/{checkIn.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.PostAsJsonAsync($"/api/v1/check-ins/{checkIn.Id}/approve", new CheckInReviewInput(null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync(SchedulePath(setup))).StatusCode);
        Assert.DoesNotContain((await setup.OtherCoach.GetFromJsonAsync<PagedResult<CheckInSummary>>("/api/v1/check-ins?pageSize=100"))!.Items, x => x.Id == checkIn.Id);
        var sibling = await ReadAsync<ClientResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Sibling " + setup.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientAreaClient(setup.CoachId, sibling.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.GetAsync($"/api/v1/me/check-ins/{checkIn.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/check-ins")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.GetAsync("/api/v1/me/check-ins")).StatusCode);

        // Transfer (D7): the check-ins follow the client; the schedule (the old coach's form) ends.
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{setup.ClientId}/transfer", new ClientTransferInput(setup.OtherCoachId))).EnsureSuccessStatusCode();
        Assert.Equal(checkIn.Id, (await setup.OtherCoach.GetFromJsonAsync<CheckInDetail>($"/api/v1/check-ins/{checkIn.Id}"))!.CheckIn.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await setup.OtherCoach.GetAsync(SchedulePath(setup))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync($"/api/v1/check-ins/{checkIn.Id}")).StatusCode);

        // Deleting the client removes its check-ins with their submissions.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{setup.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/check-ins/{checkIn.Id}")).StatusCode);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static string SchedulePath(Setup setup) => $"/api/v1/clients/{setup.ClientId}/check-in-schedule";

    private async Task<ClientResponse> ClientAsync(Setup setup) =>
        (await setup.Coach.GetFromJsonAsync<ClientDetailResponse>($"/api/v1/clients/{setup.ClientId}"))!.Client;

    private static MyFormSubmissionInput Answers(Setup setup, decimal weight, Guid? photo, string? comment = null)
    {
        var answers = new List<AnswerInput>
        {
            new(setup.WeightQuestion, JsonSerializer.SerializeToElement(weight), null),
            new(setup.EnergyQuestion, JsonSerializer.SerializeToElement(7), null),
        };
        if (photo is { } media) answers.Add(new(setup.PhotoQuestion, JsonSerializer.SerializeToElement<object?>(null), media));
        if (comment is not null) answers.Add(new(setup.CommentQuestion, JsonSerializer.SerializeToElement(comment), null));
        return new MyFormSubmissionInput(answers, Today);
    }

    private static async Task<MediaMetadata> UploadAsync(HttpClient portal)
    {
        var file = new ByteArrayContent(MeEndpointTests.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return await ReadAsync<MediaMetadata>(await portal.PostAsync("/api/v1/me/forms/media", new MultipartFormDataContent { { file, "file", "front.png" } }));
    }

    private sealed record Setup(
        Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, HttpClient Portal,
        FormVersionResponse Form, FormVersionResponse InitialForm, Guid WeightQuestion, Guid EnergyQuestion, Guid PhotoQuestion,
        Guid CommentQuestion, string Tag);

    /// <summary>A coach's published weekly check-in: weight (bound), energy, a front photo (bound) and comments.</summary>
    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoachId = await factory.CreateCoachAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Checker " + tag, null, null, null)));
        var portal = (await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id)).Http;

        async Task<FormVersionResponse> FormAsync(string name, AssessmentFormType type) =>
            await ReadAsync<FormVersionResponse>(await coach.PostAsJsonAsync("/api/v1/assessment-forms", new CreateFormInput(name, type)));
        async Task<Guid> QuestionAsync(Guid form, string text, QuestionType type, bool required, int order, QuestionMetric? metric = null) =>
            (await ReadAsync<QuestionResponse>(await coach.PostAsJsonAsync($"/api/v1/assessment-forms/{form}/questions",
                new QuestionInput(null, text, type, required, order, [], metric)))).Id;
        var checkIn = await FormAsync("Weekly check-in " + tag, AssessmentFormType.CheckIn);
        // A measure needs the matching answer type.
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync($"/api/v1/assessment-forms/{checkIn.DefinitionId}/questions",
            new QuestionInput(null, "Weight", QuestionType.ShortText, true, 0, [], QuestionMetric.WeightKg))).StatusCode);
        var weight = await QuestionAsync(checkIn.DefinitionId, "Current weight (kg)", QuestionType.Number, true, 0, QuestionMetric.WeightKg);
        var energy = await QuestionAsync(checkIn.DefinitionId, "Energy (1–10)", QuestionType.Number, true, 1);
        var photo = await QuestionAsync(checkIn.DefinitionId, "Front photo", QuestionType.MediaUpload, false, 2, QuestionMetric.FrontPhoto);
        var comment = await QuestionAsync(checkIn.DefinitionId, "Comments", QuestionType.LongText, false, 3);
        var published = await ReadAsync<FormVersionResponse>(await coach.PostAsync($"/api/v1/assessment-forms/{checkIn.DefinitionId}/publish", null));
        var initial = await FormAsync("Initial " + tag, AssessmentFormType.InitialAssessment);
        await QuestionAsync(initial.DefinitionId, "Goal", QuestionType.ShortText, true, 0);
        await ReadAsync<FormVersionResponse>(await coach.PostAsync($"/api/v1/assessment-forms/{initial.DefinitionId}/publish", null));
        return new Setup(coachId, coach, otherCoachId, factory.CreateCoachClient(otherCoachId), client.Id, portal,
            published, initial, weight, energy, photo, comment, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
