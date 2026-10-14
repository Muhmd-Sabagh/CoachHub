using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;

namespace CoachHub.IntegrationTests.WorkoutPlanning;

/// <summary>
/// P9 on SQL Server: the exercise library v2 with platform and custom exercises (§17), and typed workout
/// prescriptions with blocks, supersets, giant sets and drop sets (§16, D3).
/// </summary>
public sealed class WorkoutBuilderTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task The_library_has_muscles_equipment_and_custom_exercises()
    {
        var coachAId = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachAId);
        var coachB = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var (category, bench) = await ReferencesAsync(admin);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var muscles = (await coachA.GetFromJsonAsync<PagedResult<BilingualReferenceResponse>>("/api/v1/reference-data/muscle-groups?pageSize=100"))!;
        Assert.True(muscles.TotalCount >= 14);
        Assert.Contains(muscles.Items, x => x.NameEn == "Chest" && x.NameAr == "الصدر");

        var platform = await ReadAsync<ExerciseResponse>(await admin.PostAsJsonAsync("/api/v1/training/exercises",
            Exercise("Bench press " + tag, category) with
            {
                Difficulty = ExerciseDifficulty.Intermediate, SecondaryMuscleGroupIds = [TestWorkouts.Triceps], EquipmentIds = [bench],
                InstructionsEn = "Lower the bar to the chest.", CoachingCuesEn = "Squeeze the shoulder blades.", CommonMistakesAr = "ارتداد البار"
            }));
        Assert.Equal((false, ExerciseDifficulty.Intermediate, TestWorkouts.Chest), (platform.IsCustom, platform.Difficulty!.Value, platform.PrimaryMuscleGroupId!.Value));
        Assert.Equal([TestWorkouts.Triceps], platform.SecondaryMuscleGroupIds!);
        Assert.Equal([bench], platform.EquipmentIds!);
        var custom = await ReadAsync<ExerciseResponse>(await coachA.PostAsJsonAsync("/api/v1/training/exercises",
            Exercise("House press " + tag, category) with { PrimaryMuscleGroupId = TestWorkouts.Triceps }));
        Assert.Equal((true, coachAId), (custom.IsCustom, custom.CoachId!.Value));

        // Filters: a secondary muscle counts; equipment, difficulty and scope narrow the list.
        Assert.Equal(2, (await ListAsync(coachA, $"searchTerm={tag}&muscleGroupId={TestWorkouts.Triceps}")).TotalCount);
        Assert.Equal(platform.Id, Assert.Single((await ListAsync(coachA, $"searchTerm={tag}&equipmentId={bench}")).Items).Id);
        Assert.Equal(platform.Id, Assert.Single((await ListAsync(coachA, $"searchTerm={tag}&difficulty=Intermediate")).Items).Id);
        Assert.Equal(custom.Id, Assert.Single((await ListAsync(coachA, $"searchTerm={tag}&scope=Custom")).Items).Id);
        Assert.Equal(platform.Id, Assert.Single((await ListAsync(coachB, $"searchTerm={tag}")).Items).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/training/exercises/{custom.Id}")).StatusCode);

        // Coaches write only their own exercises; a primary muscle is required.
        Assert.Equal(HttpStatusCode.Forbidden, (await coachA.PutAsJsonAsync($"/api/v1/training/exercises/{platform.Id}",
            Exercise("Mine now", category))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.PutAsJsonAsync($"/api/v1/training/exercises/{custom.Id}",
            Exercise("Mine now", category))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coachA.PostAsJsonAsync("/api/v1/training/exercises",
            Exercise("No muscle", category) with { PrimaryMuscleGroupId = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coachA.PostAsJsonAsync("/api/v1/training/exercises",
            Exercise("Unknown muscle", category) with { SecondaryMuscleGroupIds = [Guid.NewGuid()] })).StatusCode);

        // Reference data in use cannot be deleted, nor can an exercise a plan uses.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/reference-data/muscle-groups/{TestWorkouts.Triceps}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/reference-data/equipment/{bench}")).StatusCode);
        var plan = await ReadAsync<WorkoutPlanResponse>(await coachA.PostAsJsonAsync("/api/v1/workout-plans", Plan(custom.Id, platform.Id)));
        Assert.Equal(HttpStatusCode.Conflict, (await coachA.DeleteAsync($"/api/v1/training/exercises/{custom.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coachA.DeleteAsync($"/api/v1/workout-plans/{plan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coachA.DeleteAsync($"/api/v1/training/exercises/{custom.Id}")).StatusCode);
    }

    [Fact]
    public async Task Plans_prescribe_typed_sets_in_blocks_and_groups()
    {
        var coach = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var (category, _) = await ReferencesAsync(admin);
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++)
            ids.Add((await ReadAsync<ExerciseResponse>(await coach.PostAsJsonAsync("/api/v1/training/exercises",
                Exercise($"Move {i} {Guid.NewGuid():N}", category)))).Id);

        var plan = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync("/api/v1/workout-plans", Plan(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5])));
        var day = Assert.Single(plan.Days);
        Assert.Equal([WorkoutBlock.WarmUp, WorkoutBlock.Main, WorkoutBlock.Main, WorkoutBlock.Main, WorkoutBlock.Main, WorkoutBlock.Main, WorkoutBlock.Cooldown],
            day.Exercises.Select(x => x.Block));
        Assert.Equal([null, "A1", "A2", "B1", "B2", "B3", null], day.Exercises.Select(x => x.GroupLabel));
        Assert.Equal([(SetGroupType.Superset, "A", (int?)90), (SetGroupType.GiantSet, "B", (int?)120)],
            day.SetGroups.Select(x => (x.Type, x.Label, x.RestSeconds)));
        var press = day.Exercises[1];
        Assert.Equal([SetType.WarmUp, SetType.Normal, SetType.Normal, SetType.Drop], press.Sets.Select(x => x.Type));
        Assert.Equal((8, 12, 60m, 2, 8.5m, new Tempo(3, 1, 1, 0), 90),
            (press.Sets[1].RepsMin!.Value, press.Sets[1].RepsMax!.Value, press.Sets[1].LoadKg!.Value, press.Sets[1].Rir!.Value,
             press.Sets[1].Rpe!.Value, press.Sets[1].Tempo!, press.Sets[1].RestSeconds!.Value));
        Assert.True(day.Exercises[6].Sets[0].IsSkippable);
        Assert.Equal(45, day.Exercises[6].Sets[0].DurationSeconds);

        // A new version and a copy keep the structure under new identifiers.
        var next = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/workout-plans/{plan.Id}/revisions",
            Plan(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]) with { BaseVersionNumber = 1 }));
        Assert.Equal([null, "A1", "A2", "B1", "B2", "B3", null], next.Days[0].Exercises.Select(x => x.GroupLabel));
        var copy = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/workout-plans/{plan.Id}/copies",
            new CopyWorkoutPlanInput("Copied", null, null)));
        Assert.DoesNotContain(copy.Days[0].SetGroups, x => day.SetGroups.Any(old => old.Id == x.Id));
        Assert.Equal(4, copy.Days[0].Exercises[1].Sets.Count);
        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync($"/api/v1/workout-plans/{plan.Id}/pdf/preview?language=Arabic")).StatusCode);

        // The structure is validated (§16).
        var valid = Plan(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5]);
        // A superset of three (and a giant set of two); a cool-down first; an exercise without sets; a drop set first;
        // an RPE off the half steps; superset members apart.
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order == 3 ? x with { SetGroupId = SupersetId(valid) } : x).ToArray()));
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order == 0 ? x with { Block = WorkoutBlock.Cooldown } : x).ToArray()));
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order == 1 ? x with { Sets = [] } : x).ToArray()));
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order == 1
            ? x with { Sets = [x.Sets.First() with { Type = SetType.Drop }, .. x.Sets.Skip(1)] } : x).ToArray()));
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order == 1
            ? x with { Sets = [x.Sets.First() with { Rpe = 8.3m }, .. x.Sets.Skip(1)] } : x).ToArray()));
        await AssertInvalidAsync(coach, Change(valid, exercises => exercises.Select(x => x.Order switch
        {
            2 => x with { Order = 4 },
            4 => x with { Order = 2 },
            _ => x
        }).ToArray()));
    }

    [Fact]
    public async Task A_transferred_clients_plan_keeps_the_previous_coachs_custom_exercise()
    {
        var coachAId = await factory.CreateCoachAsync();
        var coachBId = await factory.CreateCoachAsync();
        var coachA = factory.CreateCoachClient(coachAId);
        var coachB = factory.CreateCoachClient(coachBId);
        using var admin = factory.CreateAdminClient();
        var (category, _) = await ReferencesAsync(admin);
        var mine = await ReadAsync<ExerciseResponse>(await coachA.PostAsJsonAsync("/api/v1/training/exercises", Exercise("A's lift", category)));
        var other = await ReadAsync<ExerciseResponse>(await coachA.PostAsJsonAsync("/api/v1/training/exercises", Exercise("A's other lift", category)));
        var client = await ReadAsync<ClientResponse>(await coachA.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Moving lifter", null, null, null)));
        var plan = await ReadAsync<WorkoutPlanResponse>(await coachA.PostAsJsonAsync("/api/v1/workout-plans", Simple(mine.Id) with { ClientId = client.Id }));
        (await admin.PostAsJsonAsync($"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachBId))).EnsureSuccessStatusCode();

        var moved = (await coachB.GetFromJsonAsync<WorkoutPlanResponse>($"/api/v1/workout-plans/{plan.Id}"))!;
        Assert.Equal("A's lift", moved.Days[0].Exercises[0].ExerciseNameEn);
        Assert.Equal(HttpStatusCode.OK, (await coachB.PutAsJsonAsync($"/api/v1/workout-plans/{plan.Id}/revisions/current",
            Simple(mine.Id) with { ClientId = client.Id, BaseVersionNumber = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coachB.PutAsJsonAsync($"/api/v1/workout-plans/{plan.Id}/revisions/current",
            Simple(other.Id) with { ClientId = client.Id })).StatusCode);
    }

    private static readonly Guid DayId = new("5a1f0000-0000-4000-8000-000000000001");

    /// <summary>A day with two exercises.</summary>
    private static WorkoutPlanInput Plan(Guid first, Guid second) => Simple(first) with
    {
        Days = [Simple(first).Days.Single() with
        {
            Exercises = [.. Simple(first).Days.Single().Exercises, new(Guid.NewGuid(), second, 1, null, TestWorkouts.Sets(3, 8, 12, 90))]
        }]
    };

    /// <summary>
    /// A day of seven exercises: a warm-up; superset A (a press with a warm-up set, two working sets and a drop set;
    /// a row); giant set B (squat, lunge, squat again); a skippable, timed cool-down.
    /// </summary>
    private static WorkoutPlanInput Plan(Guid warmUp, Guid press, Guid row, Guid squat, Guid lunge, Guid stretch)
    {
        Guid superset = Guid.NewGuid(), giant = Guid.NewGuid();
        PrescribedSetInput[] pressSets =
        [
            new(Guid.NewGuid(), 0, SetType.WarmUp, 15, null, 20),
            new(Guid.NewGuid(), 1, SetType.Normal, 8, 12, 60, 2, 8.5m, new Tempo(3, 1, 1, 0), 90),
            new(Guid.NewGuid(), 2, SetType.Normal, 8, 12, 60, 2, 8.5m, new Tempo(3, 1, 1, 0), 90),
            new(Guid.NewGuid(), 3, SetType.Drop, 10, null, 45, 0, 10),
        ];
        return new WorkoutPlanInput("Structured " + Guid.NewGuid().ToString("N")[..6], null, null, [],
        [
            new(DayId, "Upper", null, null, null, 0,
            [
                new(Guid.NewGuid(), warmUp, 0, null, TestWorkouts.Sets(1, 20), WorkoutBlock.WarmUp),
                new(Guid.NewGuid(), press, 1, "Pause on the chest", pressSets, WorkoutBlock.Main, superset),
                new(Guid.NewGuid(), row, 2, null, TestWorkouts.Sets(3, 10, 12, loadKg: 50), WorkoutBlock.Main, superset),
                new(Guid.NewGuid(), squat, 3, null, TestWorkouts.Sets(3, 6, 8, loadKg: 100), WorkoutBlock.Main, giant),
                new(Guid.NewGuid(), lunge, 4, null, TestWorkouts.Sets(3, 10), WorkoutBlock.Main, giant),
                new(Guid.NewGuid(), squat, 5, null, TestWorkouts.Sets(2, 15, loadKg: 60), WorkoutBlock.Main, giant),
                new(Guid.NewGuid(), stretch, 6, null, [new PrescribedSetInput(Guid.NewGuid(), 0, DurationSeconds: 45, IsSkippable: true)], WorkoutBlock.Cooldown),
            ],
            [new(superset, SetGroupType.Superset, 90), new(giant, SetGroupType.GiantSet, 120)])
        ]);
    }

    private static Guid SupersetId(WorkoutPlanInput input) => input.Days.Single().SetGroups!.First().Id;

    private static WorkoutPlanInput Simple(Guid exerciseId) => new("Simple", null, null, [],
        [new(Guid.NewGuid(), "Day", null, null, null, 0, [new(Guid.NewGuid(), exerciseId, 0, null, TestWorkouts.Sets(3, 5))])]);

    private static WorkoutPlanInput Change(WorkoutPlanInput input, Func<IReadOnlyCollection<WorkoutExerciseInput>, WorkoutExerciseInput[]> change) =>
        input with { Days = [input.Days.Single() with { Exercises = change(input.Days.Single().Exercises) }] };

    private static async Task AssertInvalidAsync(HttpClient coach, WorkoutPlanInput input) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/workout-plans", input)).StatusCode);

    private static ExerciseInput Exercise(string name, Guid category) =>
        new(name, null, category, null, null, PrimaryMuscleGroupId: TestWorkouts.Chest);

    private static async Task<(Guid Category, Guid Equipment)> ReferencesAsync(HttpClient admin)
    {
        var tag = Guid.NewGuid().ToString("N");
        var category = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/exercise-categories",
            new BilingualReferenceInput("Builder " + tag, null)));
        var equipment = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/equipment",
            new BilingualReferenceInput("Bench " + tag, "بنش")));
        return (category.Id, equipment.Id);
    }

    private static async Task<PagedResult<ExerciseResponse>> ListAsync(HttpClient http, string query) =>
        (await http.GetFromJsonAsync<PagedResult<ExerciseResponse>>("/api/v1/training/exercises?" + query))!;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
