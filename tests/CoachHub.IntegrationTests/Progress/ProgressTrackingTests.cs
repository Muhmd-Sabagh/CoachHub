using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Progress;
using CoachHub.Application.Settings;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Progress;
using CoachHub.Domain.Settings;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.Progress;

/// <summary>
/// P11 on SQL Server: body tracking (§24, §25, §27), lifestyle tracking with targets (§31–§33), progress photos (§26)
/// and the progress dashboard (§37), with tenant isolation (§62), transfer (D7) and the latest weigh-in in the
/// nutrition engine.
/// </summary>
public sealed class ProgressTrackingTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private const string Me = "/api/v1/me/progress";

    [Fact]
    public async Task The_client_tracks_weight_body_fat_and_measurements()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        await PutAsync(portal, $"{Me}/weight/{Day(-14)}", new WeightInput(84m));
        await PutAsync(portal, $"{Me}/weight/{Day(-7)}", new WeightInput(83m));
        await PutAsync(portal, $"{Me}/weight/{Day(0)}", new WeightInput(82.5m));
        await PutAsync(portal, $"{Me}/weight/{Day(0)}", new WeightInput(82m, "Morning"));   // same day: replaced
        await PutAsync(portal, $"{Me}/body-fat/{Day(-14)}", new BodyFatInput(20m));
        await PutAsync(portal, $"{Me}/body-fat/{Day(0)}", new BodyFatInput(18.5m));
        await PutAsync(portal, $"{Me}/measurements/{Day(-14)}", new MeasurementsInput(
            [new(MeasurementSite.Waist, 90m), new(MeasurementSite.Chest, 100m)]));
        await PutAsync(portal, $"{Me}/measurements/{Day(0)}", new MeasurementsInput(
            [new(MeasurementSite.Waist, 87m), new(MeasurementSite.Arm, 35m)]));

        var body = (await portal.GetFromJsonAsync<BodyProgressResponse>($"{Me}/body?from={Day(-27)}&to={Day(0)}"))!;
        var weight = body.Weight.Summary;
        Assert.Equal((84m, 82m, 82m, 84m, 3), (weight.Start!.Value, weight.Current!.Value, weight.Lowest!.Value, weight.Highest!.Value, weight.Count));
        Assert.Equal((-2m, -2.4m), (weight.Change!.Value, weight.ChangePercent!.Value));
        Assert.Equal("Morning", body.Weight.Entries.Single(x => x.Date == Today).Notes);
        Assert.Equal(-1.5m, body.BodyFat.Summary.Change);
        var waist = body.Measurements.Single(x => x.Site == MeasurementSite.Waist).Summary;
        Assert.Equal((90m, 87m, -3m, -3.3m), (waist.Start!.Value, waist.Current!.Value, waist.Change!.Value, waist.ChangePercent!.Value));
        Assert.Equal(7, body.Measurements.Count);
        Assert.Equal(1, body.Measurements.Single(x => x.Site == MeasurementSite.Chest).Summary.Count);

        // A site sent without a value is removed; others stay.
        await PutAsync(portal, $"{Me}/measurements/{Day(0)}", new MeasurementsInput([new(MeasurementSite.Arm, null)]));
        body = (await portal.GetFromJsonAsync<BodyProgressResponse>($"{Me}/body?from={Day(-27)}&to={Day(0)}"))!;
        Assert.Equal(0, body.Measurements.Single(x => x.Site == MeasurementSite.Arm).Summary.Count);
        Assert.Equal(2, body.Measurements.Single(x => x.Site == MeasurementSite.Waist).Summary.Count);

        // Typed values only (D3); no future days; each site once.
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/weight/{Day(0)}", new WeightInput(10m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/weight/{Day(3)}", new WeightInput(80m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/measurements/{Day(0)}", new MeasurementsInput(
            [new(MeasurementSite.Hip, 99m), new(MeasurementSite.Hip, 98m)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.GetAsync($"{Me}/body?from={Day(-400)}&to={Day(0)}")).StatusCode);

        // Deleting today's weigh-in makes last week's current; the nutrition engine uses the latest weigh-in.
        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"{Me}/weight/{Day(0)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.DeleteAsync($"{Me}/weight/{Day(0)}")).StatusCode);
        var birth = Today.AddYears(-30).AddDays(-1);
        (await setup.Coach.PutAsJsonAsync($"/api/v1/clients/{setup.ClientId}/profile", new ClientProfileInput(
            birth, Sex.Male, 180, 80, null, FitnessGoal.LoseFat, ActivityLevel.ModeratelyActive, 4, 8000, TrainingLocation.Gym,
            null, [], null, [], null, [], null, [], null, 4, null, null, null, null))).EnsureSuccessStatusCode();
        var targets = (await setup.Coach.GetFromJsonAsync<EnergyCalculatorResponse>($"/api/v1/clients/{setup.ClientId}/nutrition-targets"))!;
        Assert.Equal(83m, targets.WeightKg);
        body = (await setup.Coach.GetFromJsonAsync<BodyProgressResponse>($"/api/v1/clients/{setup.ClientId}/progress/body?from={Day(-27)}&to={Day(0)}"))!;
        Assert.Equal((83m, 80m), (body.Weight.Summary.Current!.Value, body.ProfileStartingWeightKg!.Value));
    }

    [Fact]
    public async Task Lifestyle_counts_against_the_targets_the_coach_sets()
    {
        var setup = await SetUpAsync();
        (await setup.Coach.PutAsJsonAsync("/api/v1/settings/coach", new CoachSettingsInput(
            new ActivityFactors(1.2m, 1.375m, 1.55m, 1.725m, 1.9m), 20m, 10m, 2.0m, 0.8m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5), FoodReplacementRule.Calories,
            DefaultTargets: new LifestyleTargetValues(8000, 2000, 7m)))).EnsureSuccessStatusCode();
        var set = await ReadAsync<LifestyleTargetsResponse>(await setup.Coach.PutAsJsonAsync(
            $"/api/v1/clients/{setup.ClientId}/progress/targets", new LifestyleTargetValues(10000, null, null)));
        Assert.Equal(new LifestyleTargetValues(10000, 2000, 7m), set.Effective);
        Assert.Equal(set.Effective, (await setup.Portal.GetFromJsonAsync<LifestyleTargetsResponse>($"{Me}/targets"))!.Effective);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.PutAsJsonAsync(
            $"/api/v1/clients/{setup.ClientId}/progress/targets", new LifestyleTargetValues(10, null, null))).StatusCode);

        var portal = setup.Portal;
        await PutAsync(portal, $"{Me}/sleep/{Day(-1)}", new SleepInput(null, 4, new TimeOnly(23, 0), new TimeOnly(7, 0)));
        await PutAsync(portal, $"{Me}/sleep/{Day(0)}", new SleepInput(6m));
        await PutAsync(portal, $"{Me}/steps/{Day(-1)}", new StepsInput(12000));
        await PutAsync(portal, $"{Me}/steps/{Day(0)}", new StepsInput(6000));
        var glass = await ReadAsync<WaterEntryResponse>(await portal.PostAsJsonAsync($"{Me}/water", new WaterInput(Today, 750)));
        await ReadAsync<WaterEntryResponse>(await portal.PostAsJsonAsync($"{Me}/water", new WaterInput(Today, 750)));
        await ReadAsync<WaterEntryResponse>(await portal.PostAsJsonAsync($"{Me}/water", new WaterInput(Today.AddDays(-1), 2500)));

        var lifestyle = (await portal.GetFromJsonAsync<LifestyleResponse>($"{Me}/lifestyle?from={Day(-6)}&to={Day(0)}"))!;
        Assert.Equal(7, lifestyle.Days.Count);
        var today = lifestyle.Days.Single(x => x.Date == Today);
        Assert.Equal((1500, 2, 75m, 60m, 85.7m), (today.WaterMl, today.Water.Count, today.WaterCompletionPercent!.Value,
            today.StepsCompletionPercent!.Value, today.SleepCompletionPercent!.Value));
        Assert.Equal(8m, lifestyle.Days.Single(x => x.Date == Today.AddDays(-1)).Sleep!.Hours);
        var summary = lifestyle.Summary;
        Assert.Equal((2, 7m, 1, 50m), (summary.SleepNights, summary.AverageSleepHours!.Value, summary.SleepGoalNights, summary.SleepGoalPercent!.Value));
        Assert.Equal((9000, 1, 50m), (summary.AverageSteps!.Value, summary.StepsGoalDays, summary.StepsGoalPercent!.Value));
        Assert.Equal((2, 2000, 1, 50m), (summary.WaterDays, summary.AverageWaterMl!.Value, summary.WaterGoalDays, summary.WaterGoalPercent!.Value));
        Assert.Equal(7, lifestyle.Weeks.Sum(x => x.End.DayNumber - x.Start.DayNumber + 1));

        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"{Me}/water/{glass.Id}")).StatusCode);
        lifestyle = (await setup.Coach.GetFromJsonAsync<LifestyleResponse>(
            $"/api/v1/clients/{setup.ClientId}/progress/lifestyle?from={Day(-6)}&to={Day(0)}"))!;
        Assert.Equal(750, lifestyle.Days.Single(x => x.Date == Today).WaterMl);

        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/sleep/{Day(0)}",
            new SleepInput(Bedtime: new TimeOnly(23, 0)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/steps/{Day(0)}", new StepsInput(-1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync($"{Me}/water", new WaterInput(Today, 0))).StatusCode);
    }

    [Fact]
    public async Task Photos_and_the_dashboard_stay_with_the_client_and_their_coach()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        var latest = await UploadPhotoAsync(portal, Today, PhotoPose.Front);
        var first = await UploadPhotoAsync(portal, Today.AddDays(-30), PhotoPose.Front);
        await PutAsync(portal, $"{Me}/weight/{Day(0)}", new WeightInput(80m));
        var photos = (await portal.GetFromJsonAsync<ProgressPhotoResponse[]>($"{Me}/photos"))!;
        Assert.Equal([latest.Id, first.Id], photos.Select(x => x.Id));

        // The image is the client's own media: the client and their coach open it; nobody else does (§26, §62).
        var sibling = await ReadAsync<ClientResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Sibling " + setup.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientPortalClient(setup.CoachId, sibling.Id);
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await setup.Coach.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
        Assert.Empty((await siblingPortal.GetFromJsonAsync<ProgressPhotoResponse[]>($"{Me}/photos"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.DeleteAsync($"{Me}/photos/{latest.Id}")).StatusCode);
        Assert.Equal(0, (await siblingPortal.GetFromJsonAsync<BodyProgressResponse>($"{Me}/body?from={Day(-6)}&to={Day(0)}"))!.Weight.Summary.Count);

        // Photo details can change; a photo is deleted with its image.
        var side = await ReadAsync<ProgressPhotoResponse>(await portal.PutAsJsonAsync($"{Me}/photos/{first.Id}",
            new ProgressPhotoInput(first.Date, PhotoPose.Side, null, "Week 1")));
        Assert.Equal((PhotoPose.Side, "Week 1"), (side.Pose, side.Notes));
        var extra = await UploadPhotoAsync(portal, Today, PhotoPose.Custom, "flexed");
        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"{Me}/photos/{extra.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync($"/api/v1/media/{extra.MediaId}")).StatusCode);

        // The dashboard (§37) assembles body, nutrition, training and lifestyle, for the client and the coach.
        var dashboard = (await setup.Coach.GetFromJsonAsync<ProgressDashboardResponse>(
            $"/api/v1/clients/{setup.ClientId}/progress/dashboard?from={Day(-27)}&to={Day(0)}"))!;
        Assert.Equal((80m, 2, first.Id, latest.Id), (dashboard.Body.Weight.Current!.Value, dashboard.Body.PhotoCount,
            dashboard.Body.FirstPhoto!.Id, dashboard.Body.LatestPhoto!.Id));
        Assert.Equal((28, 0, 0), (dashboard.Nutrition.Days, dashboard.Nutrition.LoggedDays, dashboard.Training.Summary.CompletedSessions));
        Assert.Equal(0, dashboard.Lifestyle.StepsDays);
        Assert.Equal(dashboard.Body.PhotoCount, (await portal.GetFromJsonAsync<ProgressDashboardResponse>(
            $"{Me}/dashboard?from={Day(-27)}&to={Day(0)}"))!.Body.PhotoCount);

        // Roles stay in their lanes; another coach finds nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync(
            $"/api/v1/clients/{setup.ClientId}/progress/dashboard?from={Day(-6)}&to={Day(0)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.PutAsJsonAsync(
            $"/api/v1/clients/{setup.ClientId}/progress/targets", new LifestyleTargetValues(9000, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync($"/api/v1/clients/{setup.ClientId}/progress/photos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.GetAsync($"{Me}/photos")).StatusCode);

        // Progress, photos and their images follow the client to a new coach (D7).
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{setup.ClientId}/transfer", new ClientTransferInput(setup.OtherCoachId))).EnsureSuccessStatusCode();
        Assert.Equal(2, (await setup.OtherCoach.GetFromJsonAsync<ProgressPhotoResponse[]>($"/api/v1/clients/{setup.ClientId}/progress/photos"))!.Length);
        Assert.Equal(HttpStatusCode.OK, (await setup.OtherCoach.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync($"/api/v1/clients/{setup.ClientId}/progress/photos")).StatusCode);

        // Deleting the client removes their photos and images with them.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{setup.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/media/{latest.MediaId}")).StatusCode);
    }

    private static string Day(int offset) => Today.AddDays(offset).ToString("yyyy-MM-dd");

    private static async Task PutAsync<T>(HttpClient http, string path, T input)
    {
        var response = await http.PutAsJsonAsync(path, input);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<ProgressPhotoResponse> UploadPhotoAsync(HttpClient portal, DateOnly date, PhotoPose pose, string? label = null)
    {
        var file = new ByteArrayContent(MeEndpointTests.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent
        {
            { file, "file", "front.png" },
            { new StringContent(date.ToString("yyyy-MM-dd")), "date" },
            { new StringContent(pose.ToString()), "pose" },
        };
        if (label is not null) form.Add(new StringContent(label), "label");
        return await ReadAsync<ProgressPhotoResponse>(await portal.PostAsync($"{Me}/photos", form));
    }

    private sealed record Setup(Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, HttpClient Portal, string Tag);

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = factory.CreateCoachClient(coachId);
        var otherCoachId = await factory.CreateCoachAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Progress " + tag, null, null, null)));
        // A real client account: photo uploads record their uploader, which must exist.
        var portal = (await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id)).Http;
        return new Setup(coachId, coach, otherCoachId, factory.CreateCoachClient(otherCoachId), client.Id, portal, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
