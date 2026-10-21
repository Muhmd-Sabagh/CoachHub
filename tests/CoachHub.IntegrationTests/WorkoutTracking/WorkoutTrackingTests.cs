using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Settings;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Settings;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using CoachHub.IntegrationTests.WorkoutPlanning;

namespace CoachHub.IntegrationTests.WorkoutTracking;

/// <summary>
/// P10 on SQL Server: the client logs workouts against the plan's weekly rotation (§18), with history (§19), PRs
/// (§21, §22) and training analytics (§20, §23); coaches read them; tenant isolation (§62) and transfer (D7).
/// </summary>
public sealed class WorkoutTrackingTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private const string Me = "/api/v1/me/training";

    [Fact]
    public async Task The_client_logs_a_planned_workout_and_sets_records()
    {
        var setup = await SetUpAsync();
        var portal = setup.Portal;
        var day = $"{Me}/days/{Today:yyyy-MM-dd}";

        // Today's workout: the rotation of the plan in force, starting with its first day.
        var start = (await portal.GetFromJsonAsync<TrainingDayResponse>(day))!;
        Assert.Null(start.InProgress);
        Assert.Equal((setup.Plan.Id, 2, setup.PushDayId), (start.Plan!.PlanId, start.Plan.Days.Count, start.Plan.SuggestedDayId!.Value));
        Assert.Equal((2, 5), (start.Plan.Days[0].ExerciseCount, start.Plan.Days[0].SetCount));
        Assert.Equal((2, 0), (start.Week.PlannedSessions, start.Week.CompletedSessions));

        // Start the push day: its exercises with what the plan prescribes.
        var session = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today, setup.PushDayId)));
        Assert.Equal((WorkoutSessionStatus.InProgress, "Push", true, 1), (session.Status, session.NameEn, session.PlanAvailable, session.VersionNumber!.Value));
        var bench = session.Exercises[0];
        var plank = session.Exercises[1];
        Assert.Equal((setup.Bench.Id, 4, false), (bench.ExerciseId, bench.Planned.Count, bench.CanSkip));
        Assert.Equal((WorkoutBlock.Cooldown, true), (plank.Block, plank.CanSkip));
        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today))).StatusCode);

        // Skip only what the coach permits (§18).
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PutAsJsonAsync($"{Me}/sessions/{session.Id}/exercises/{bench.Id}/skip",
            new SkipLoggedExerciseInput(true))).StatusCode);
        session = await ReadAsync<WorkoutSessionResponse>(await portal.PutAsJsonAsync($"{Me}/sessions/{session.Id}/exercises/{plank.Id}/skip",
            new SkipLoggedExerciseInput(true)));
        Assert.True(session.Exercises[1].IsSkipped);
        Assert.Equal(HttpStatusCode.BadRequest, (await LogAsync(portal, session.Id, bench.Id,
            new LoggedSetInput(bench.Planned[1].Id, Status: LoggedSetStatus.Skipped))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LogAsync(portal, session.Id, bench.Id,
            new LoggedSetInput(Guid.NewGuid(), Reps: 8, WeightKg: 60))).StatusCode);

        // Log the warm-up and three working sets (sets, reps, weight, RIR, RPE).
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id, new LoggedSetInput(bench.Planned[0].Id, SetType.WarmUp, Reps: 10, WeightKg: 20)));
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id, new LoggedSetInput(bench.Planned[1].Id, Reps: 8, WeightKg: 60, Rir: 2)));
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id, new LoggedSetInput(bench.Planned[2].Id, Reps: 8, WeightKg: 60, Rir: 1)));
        session = await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id,
            new LoggedSetInput(bench.Planned[3].Id, Reps: 6, WeightKg: 65, Rpe: 9)));
        Assert.Equal(HttpStatusCode.BadRequest, (await LogAsync(portal, session.Id, bench.Id,
            new LoggedSetInput(bench.Planned[3].Id, Reps: 6, WeightKg: 65))).StatusCode);
        var logged = session.Exercises[0];
        Assert.Equal(4, logged.Sets.Count);
        Assert.Equal(1350m, logged.VolumeKg);                    // 2 × 8 × 60 + 6 × 65; the warm-up is not volume
        Assert.Equal(78m, logged.Sets[3].EstimatedOneRepMaxKg);  // Epley: 65 × (1 + 6/30)
        Assert.Null(logged.Sets[0].EstimatedOneRepMaxKg);

        // An exercise from the library: the coach's custom one; it cannot be deleted while logged.
        session = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions/{session.Id}/exercises",
            new AddLoggedExerciseInput(setup.Curl.Id)));
        var curl = session.Exercises[2];
        Assert.Equal((setup.Curl.Id, (Guid?)null, false), (curl.ExerciseId, curl.PlannedExerciseId, curl.CanSkip));
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, curl.Id, new LoggedSetInput(Reps: 12, WeightKg: 15)));
        Assert.Equal(HttpStatusCode.Conflict, (await setup.Coach.DeleteAsync($"/api/v1/training/exercises/{setup.Curl.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync($"{Me}/sessions/{session.Id}/exercises",
            new AddLoggedExerciseInput(setup.OtherCoachExercise.Id))).StatusCode);

        // Complete: the first workout sets the first records (§21).
        session = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions/{session.Id}/complete",
            new WorkoutNotesInput("Felt strong")));
        Assert.Equal((WorkoutSessionStatus.Completed, "Felt strong"), (session.Status, session.Notes));
        Assert.Equal(8, session.Records.Count);
        var weight = session.Records.Single(x => x.ExerciseId == setup.Bench.Id && x.Type == PersonalRecordType.Weight);
        Assert.Equal((65m, (decimal?)null, 65m, 6), (weight.Value, weight.PreviousValue, weight.WeightKg!.Value, weight.Reps!.Value));
        Assert.Equal(1350m, session.Records.Single(x => x.ExerciseId == setup.Bench.Id && x.Type == PersonalRecordType.Volume).Value);
        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsync($"{Me}/sessions/{session.Id}/complete", null)).StatusCode);

        // The week moves on to the next day of the rotation.
        var after = (await portal.GetFromJsonAsync<TrainingDayResponse>(day))!;
        Assert.Equal((setup.LegsDayId, 1, true), (after.Plan!.SuggestedDayId!.Value, after.Week.CompletedSessions, after.Plan.Days[0].DoneThisWeek));
        Assert.Equal(session.Id, Assert.Single(after.Sessions).Id);

        // A heavier free workout: a new weight and e1RM record, with the previous best.
        var free = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today, NameEn: "Extra bench")));
        Assert.False(free.PlanAvailable);
        free = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions/{free.Id}/exercises", new AddLoggedExerciseInput(setup.Bench.Id)));
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, free.Id, free.Exercises[0].Id, new LoggedSetInput(Reps: 8, WeightKg: 70)));
        free = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsync($"{Me}/sessions/{free.Id}/complete", null));
        Assert.Equal([PersonalRecordType.Weight, PersonalRecordType.EstimatedOneRepMax], free.Records.Select(x => x.Type));
        Assert.Equal((70m, 65m), (free.Records[0].Value, free.Records[0].PreviousValue!.Value));
        Assert.Equal(88.67m, free.Records[1].Value);

        // History per exercise (§19), newest first, with the PR history; the coach sees the same.
        var history = (await portal.GetFromJsonAsync<ExerciseHistoryResponse>($"{Me}/exercises/{setup.Bench.Id}/history"))!;
        Assert.Equal([free.Id, session.Id], history.Entries.Select(x => x.WorkoutSessionId));
        Assert.Equal((3, 22, 1350m, 65m), (history.Entries[1].Sets, history.Entries[1].Reps, history.Entries[1].VolumeKg, history.Entries[1].TopWeightKg!.Value));
        Assert.Equal(2, history.Records.Count(x => x.Type == PersonalRecordType.Weight));
        var coachHistory = (await setup.Coach.GetFromJsonAsync<ExerciseHistoryResponse>(
            $"/api/v1/clients/{setup.ClientId}/training/exercises/{setup.Bench.Id}/history"))!;
        Assert.Equal(history.Entries.Count, coachHistory.Entries.Count);

        // Correcting a completed workout rebuilds the records: 50 kg is no longer a weight record.
        var setId = free.Exercises[0].Sets[0].Id;
        free = await ReadAsync<WorkoutSessionResponse>(await portal.PutAsJsonAsync($"{Me}/sessions/{free.Id}/sets/{setId}",
            new LoggedSetInput(Reps: 8, WeightKg: 50)));
        Assert.Empty(free.Records);
        var records = (await portal.GetFromJsonAsync<PersonalRecordsResponse>($"{Me}/records"))!;
        Assert.Equal(65m, records.Current.Single(x => x.ExerciseId == setup.Bench.Id && x.Type == PersonalRecordType.Weight).Value);
        Assert.Equal(8, records.Current.Count);

        // Deleting a workout removes it from the history.
        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"{Me}/sessions/{free.Id}")).StatusCode);
        history = (await portal.GetFromJsonAsync<ExerciseHistoryResponse>($"{Me}/exercises/{setup.Bench.Id}/history"))!;
        Assert.Equal(session.Id, Assert.Single(history.Entries).WorkoutSessionId);
        var sessions = (await portal.GetFromJsonAsync<PagedResult<WorkoutSessionSummary>>($"{Me}/sessions"))!;
        Assert.Equal((1, 5), (sessions.TotalCount, sessions.Items[0].CompletedSets));

        // A future day cannot be logged.
        Assert.Equal(HttpStatusCode.BadRequest, (await portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today.AddDays(3)))).StatusCode);
    }

    [Fact]
    public async Task Analytics_report_volume_adherence_and_muscles()
    {
        var setup = await SetUpAsync();
        (await setup.Coach.PutAsJsonAsync("/api/v1/settings/coach", new CoachSettingsInput(
            new ActivityFactors(1.2m, 1.375m, 1.55m, 1.725m, 1.9m), 20m, 10m, 2.0m, 0.8m, MacroMode.GramsPerKg,
            new AdherenceWeights(35, 35, 15, 5, 5, 5), FoodReplacementRule.Calories, OneRepMaxFormula.Brzycki, 0.5m))).EnsureSuccessStatusCode();
        var saved = (await setup.Coach.GetFromJsonAsync<CoachSettingsResponse>("/api/v1/settings/coach"))!;
        Assert.Equal((OneRepMaxFormula.Brzycki, 0.5m), (saved.OneRepMaxFormula, saved.SecondaryMuscleWeight));

        var portal = setup.Portal;
        var session = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today, setup.PushDayId)));
        var bench = session.Exercises[0];
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id, new LoggedSetInput(bench.Planned[1].Id, Reps: 10, WeightKg: 50)));
        await ReadAsync<WorkoutSessionResponse>(await LogAsync(portal, session.Id, bench.Id, new LoggedSetInput(bench.Planned[2].Id, Reps: 10, WeightKg: 50)));
        session = await ReadAsync<WorkoutSessionResponse>(await portal.PostAsync($"{Me}/sessions/{session.Id}/complete", null));
        Assert.Equal(66.67m, session.Records.Single(x => x.Type == PersonalRecordType.EstimatedOneRepMax).Value); // 50 × 36 / 27

        var path = $"/api/v1/clients/{setup.ClientId}/training/analytics?from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}";
        var analytics = (await setup.Coach.GetFromJsonAsync<TrainingAnalyticsResponse>(path))!;
        Assert.Equal((OneRepMaxFormula.Brzycki, 0.5m), (analytics.OneRepMaxFormula, analytics.SecondaryMuscleWeight));
        var summary = analytics.Summary;
        // Seven days of a two-day rotation expect two workouts; one is done.
        Assert.Equal((1, 1, 2, 50m), (summary.CompletedSessions, summary.TrainingDays, summary.ScheduledSessions, summary.AdherencePercent!.Value));
        Assert.Equal((2, 20, 1000m), (summary.Sets, summary.Reps, summary.VolumeKg));
        // Chest is the primary muscle; triceps counts half of each set as a secondary muscle.
        Assert.Equal([("Chest", 2m, 1000m), ("Triceps", 1m, 500m)], summary.Muscles.Select(x => (x.NameEn, x.Sets, x.VolumeKg)));
        Assert.Equal(1, analytics.Weeks.Sum(x => x.CompletedSessions));
        Assert.Equal(1, analytics.Months.Sum(x => x.CompletedSessions));
        var progress = Assert.Single(analytics.Exercises);
        Assert.Equal((setup.Bench.Id, 50m, 10, 66.67m), (progress.ExerciseId, progress.Points[0].TopWeightKg!.Value,
            progress.Points[0].TopWeightReps!.Value, progress.Points[0].BestEstimatedOneRepMaxKg!.Value));

        // The client sees the same analytics; ranges are bounded.
        var own = (await portal.GetFromJsonAsync<TrainingAnalyticsResponse>($"{Me}/analytics?from={Today.AddDays(-6):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"))!;
        Assert.Equal(summary.VolumeKg, own.Summary.VolumeKg);
        Assert.Equal(HttpStatusCode.BadRequest, (await setup.Coach.GetAsync(
            $"/api/v1/clients/{setup.ClientId}/training/analytics?from={Today.AddDays(-400):yyyy-MM-dd}&to={Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Workouts_are_private_to_the_client_and_their_coach()
    {
        var setup = await SetUpAsync();
        var session = await ReadAsync<WorkoutSessionResponse>(await setup.Portal.PostAsJsonAsync($"{Me}/sessions", new StartWorkoutInput(Today, setup.PushDayId)));
        var bench = session.Exercises[0];
        session = await ReadAsync<WorkoutSessionResponse>(await LogAsync(setup.Portal, session.Id, bench.Id, new LoggedSetInput(Reps: 5, WeightKg: 80)));
        session = await ReadAsync<WorkoutSessionResponse>(await setup.Portal.PostAsync($"{Me}/sessions/{session.Id}/complete", null));
        var setId = session.Exercises[0].Sets[0].Id;
        var coachPath = $"/api/v1/clients/{setup.ClientId}/training";

        // Another client of the same coach sees none of it and cannot change it.
        var sibling = await ReadAsync<ClientResponse>(await setup.Coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Sibling " + setup.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientPortalClient(setup.CoachId, sibling.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.GetAsync($"{Me}/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.PutAsJsonAsync($"{Me}/sessions/{session.Id}/sets/{setId}",
            new LoggedSetInput(Reps: 1, WeightKg: 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.DeleteAsync($"{Me}/sessions/{session.Id}")).StatusCode);
        Assert.Empty((await siblingPortal.GetFromJsonAsync<PersonalRecordsResponse>($"{Me}/records"))!.Current);
        Assert.Null((await siblingPortal.GetFromJsonAsync<TrainingDayResponse>($"{Me}/days/{Today:yyyy-MM-dd}"))!.Plan);

        // The coach reads; another coach finds nothing; roles stay in their lanes.
        var coachView = (await setup.Coach.GetFromJsonAsync<WorkoutSessionResponse>($"{coachPath}/sessions/{session.Id}"))!;
        Assert.Equal(400m, coachView.VolumeKg);
        Assert.Equal(1, (await setup.Coach.GetFromJsonAsync<PagedResult<WorkoutSessionSummary>>($"{coachPath}/sessions"))!.TotalCount);
        Assert.Equal(4, (await setup.Coach.GetFromJsonAsync<PersonalRecordsResponse>($"{coachPath}/records"))!.Current.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync(
            $"/api/v1/clients/{sibling.Id}/training/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"{coachPath}/sessions/{session.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OtherCoach.GetAsync($"{coachPath}/records")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Portal.GetAsync($"{coachPath}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Coach.GetAsync($"{Me}/sessions")).StatusCode);
        using var admin = factory.CreateAdminClient();
        Assert.Equal(session.Id, (await admin.GetFromJsonAsync<WorkoutSessionResponse>($"{coachPath}/sessions/{session.Id}"))!.Id);

        // Workouts and records follow the client to a new coach (D7).
        (await admin.PostAsJsonAsync($"/api/v1/clients/{setup.ClientId}/transfer", new ClientTransferInput(setup.OtherCoachId))).EnsureSuccessStatusCode();
        Assert.Equal(session.Id, (await setup.OtherCoach.GetFromJsonAsync<WorkoutSessionResponse>($"{coachPath}/sessions/{session.Id}"))!.Id);
        Assert.Equal(4, (await setup.OtherCoach.GetFromJsonAsync<PersonalRecordsResponse>($"{coachPath}/records"))!.Current.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Coach.GetAsync($"{coachPath}/sessions/{session.Id}")).StatusCode);
    }

    private static Task<HttpResponseMessage> LogAsync(HttpClient portal, Guid sessionId, Guid exerciseId, LoggedSetInput input) =>
        portal.PostAsJsonAsync($"{Me}/sessions/{sessionId}/exercises/{exerciseId}/sets", input);

    private sealed record Setup(
        Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, HttpClient Portal,
        WorkoutPlanResponse Plan, Guid PushDayId, Guid LegsDayId,
        ExerciseResponse Bench, ExerciseResponse Curl, ExerciseResponse OtherCoachExercise, string Tag);

    /// <summary>
    /// A coach's client with a two-day rotation: Push (bench press: a warm-up and three working sets; a skippable,
    /// timed plank) and Legs (squat). Bench press trains the chest, with the triceps as a secondary muscle.
    /// </summary>
    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = factory.CreateCoachClient(coachId);
        var otherCoachId = await factory.CreateCoachAsync();
        var otherCoach = factory.CreateCoachClient(otherCoachId);
        using var admin = factory.CreateAdminClient();
        var category = (await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/exercise-categories",
            new BilingualReferenceInput("Tracking " + tag, null)))).Id;

        async Task<ExerciseResponse> ExerciseAsync(HttpClient http, string name, Guid muscle, Guid[]? secondary = null) =>
            await ReadAsync<ExerciseResponse>(await http.PostAsJsonAsync("/api/v1/training/exercises",
                new ExerciseInput($"{name} {tag}", null, category, null, null, PrimaryMuscleGroupId: muscle, SecondaryMuscleGroupIds: secondary)));
        var bench = await ExerciseAsync(admin, "Bench press", TestWorkouts.Chest, [TestWorkouts.Triceps]);
        var plank = await ExerciseAsync(admin, "Plank", TestWorkouts.Glutes);
        var squat = await ExerciseAsync(admin, "Squat", TestWorkouts.Quadriceps);
        var curl = await ExerciseAsync(coach, "Cable curl", TestWorkouts.Triceps);
        var otherExercise = await ExerciseAsync(otherCoach, "Other curl", TestWorkouts.Triceps);

        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Lifter " + tag, null, null, null)));
        Guid push = Guid.NewGuid(), legs = Guid.NewGuid();
        PrescribedSetInput[] benchSets =
        [
            new(Guid.NewGuid(), 0, SetType.WarmUp, 10, null, 20),
            .. TestWorkouts.Sets(3, 6, 8, 90, 60).Select((set, index) => set with { Order = index + 1 }),
        ];
        var input = new WorkoutPlanInput($"Rotation {tag}", null, client.Id, [],
        [
            new(push, "Push", null, null, null, 0,
            [
                new(Guid.NewGuid(), bench.Id, 0, null, benchSets),
                new(Guid.NewGuid(), plank.Id, 1, null, [new PrescribedSetInput(Guid.NewGuid(), 0, DurationSeconds: 60, IsSkippable: true)],
                    WorkoutBlock.Cooldown),
            ]),
            new(legs, "Legs", null, null, null, 1, [new(Guid.NewGuid(), squat.Id, 0, null, TestWorkouts.Sets(3, 5, loadKg: 100))]),
        ]);
        var plan = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync("/api/v1/workout-plans", input));
        return new Setup(coachId, coach, otherCoachId, otherCoach, client.Id, factory.CreateClientPortalClient(coachId, client.Id),
            plan, push, legs, bench, curl, otherExercise, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
