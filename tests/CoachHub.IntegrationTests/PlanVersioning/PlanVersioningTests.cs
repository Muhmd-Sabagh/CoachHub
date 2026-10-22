using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Nutrition;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.Nutrition;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using CoachHub.IntegrationTests.WorkoutPlanning;

namespace CoachHub.IntegrationTests.PlanVersioning;

/// <summary>
/// D4 / §59 on SQL Server: save in place vs. save as new version, read-only history, program dates,
/// version-aware delivery and lists, and isolation between coaches.
/// </summary>
public sealed class PlanVersioningTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly October = new(2026, 10, 1);

    [Fact]
    public async Task Diet_saves_in_place_or_as_a_new_version_and_keeps_the_old_one_read_only()
    {
        var coach = await factory.CreateCoachClientAsync();
        var (oats, _) = await FoodsAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Versioned client", null, null, null)));
        var input = Diet(oats.Id, 100) with
        {
            ClientId = client.Id, StartDate = October, EndDate = October.AddDays(27), Status = PlanRevisionStatus.Draft
        };

        var created = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", input));
        Assert.Equal((1, PlanRevisionStatus.Draft, 1), (created.Revision.VersionNumber, created.Revision.Status, created.CurrentVersionNumber));
        Assert.Equal(October.AddDays(27), created.Revision.EndDate);
        Assert.NotNull(created.Revision.CreatedByUserId);

        // Save in place: still version 1, new content.
        var inPlace = await ReadAsync<DietPlanResponse>(await coach.PutAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions/current",
            Edit(created, oats.Id, 150) with { Status = PlanRevisionStatus.Active, BaseVersionNumber = 1 }));
        Assert.Equal((1, PlanRevisionStatus.Active), (inPlace.Revision.VersionNumber, inPlace.Revision.Status));
        Assert.Equal(150m, inPlace.Totals.Calories);

        // Save as new version: version 2 with fresh rows; version 1 archived unchanged.
        var saved = await coach.PostAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions",
            Edit(inPlace, oats.Id, 200) with { StartDate = October.AddDays(28), EndDate = null, BaseVersionNumber = 1 });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.EndsWith($"/api/v1/diet-plans/{created.Id}/revisions/2", saved.Headers.Location!.AbsolutePath);
        var second = await ReadAsync<DietPlanResponse>(saved);
        Assert.Equal((2, 2, 200m), (second.Revision.VersionNumber, second.CurrentVersionNumber, second.Totals.Calories));
        Assert.NotEqual(inPlace.Variants[0].Id, second.Variants[0].Id);
        Assert.NotEqual(inPlace.Notes[0].Id, second.Notes[0].Id);

        var first = await coach.GetFromJsonAsync<DietPlanResponse>($"/api/v1/diet-plans/{created.Id}/revisions/1");
        Assert.Equal((PlanRevisionStatus.Archived, true, false, 2), (first!.Revision.Status, first.Revision.IsReadOnly, first.Revision.IsCurrent, first.CurrentVersionNumber));
        Assert.Equal(150m, first.Totals.Calories);
        Assert.Equal(inPlace.Variants[0].Meals[0].FoodItems[0].Id, first.Variants[0].Meals[0].FoodItems[0].Id);
        Assert.NotNull(first.Revision.ArchivedAt);

        var current = await coach.GetFromJsonAsync<DietPlanResponse>($"/api/v1/diet-plans/{created.Id}");
        Assert.Equal(2, current!.Revision.VersionNumber);
        var history = (await coach.GetFromJsonAsync<PlanRevisionInfo[]>($"/api/v1/diet-plans/{created.Id}/revisions"))!;
        Assert.Equal([2, 1], history.Select(x => x.VersionNumber));
        Assert.Equal([PlanRevisionStatus.Active, PlanRevisionStatus.Archived], history.Select(x => x.Status));
        Assert.Equal(HttpStatusCode.NotFound, (await coach.GetAsync($"/api/v1/diet-plans/{created.Id}/revisions/3")).StatusCode);

        // An editor still on version 1 cannot overwrite or fork version 2.
        var stale = Edit(second, oats.Id, 300) with { BaseVersionNumber = 1 };
        Assert.Equal(HttpStatusCode.Conflict, (await coach.PutAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions/current", stale)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await coach.PostAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions", stale)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PutAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions/current",
            Edit(second, oats.Id, 300) with { Status = PlanRevisionStatus.Archived })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions",
            Edit(second, oats.Id, 300) with { StartDate = October, EndDate = October.AddDays(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await coach.PutAsJsonAsync($"/api/v1/diet-plans/{created.Id}", stale)).StatusCode);

        // Lists show the current version only.
        var listed = await coach.GetFromJsonAsync<PagedResult<SavedPlanSummary>>("/api/v1/saved-plans?planType=Diet&planName=Versioned");
        var row = Assert.Single(listed!.Items);
        Assert.Equal((2, PlanRevisionStatus.Active, October.AddDays(28), 200m), (row.VersionNumber, row.VersionStatus, row.StartDate, row.TotalCalories));

        // Deleting the plan removes its whole history.
        Assert.Equal(HttpStatusCode.NoContent, (await coach.DeleteAsync($"/api/v1/diet-plans/{created.Id}")).StatusCode);
        await using var context = factory.Database.CreateContext();
        Assert.False(await context.Set<DietPlanRevision>().AnyAsync(x => x.PlanId == created.Id));
        Assert.False(await context.Set<MealFoodItem>().AnyAsync(x => x.Id == first.Variants[0].Meals[0].FoodItems[0].Id));
    }

    [Fact]
    public async Task Archived_versions_are_read_only_in_the_database_itself()
    {
        var coach = await factory.CreateCoachClientAsync();
        var (oats, eggs) = await FoodsAsync();
        var created = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", Diet(oats.Id, 100)));
        await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/diet-plans/{created.Id}/revisions", Edit(created, eggs.Id, 50)));
        var archivedRowId = created.Variants[0].Meals[0].FoodItems[0].Id;

        await using (var context = factory.Database.CreateContext())
        {
            var row = await context.Set<MealFoodItem>().SingleAsync(x => x.Id == archivedRowId);
            context.Remove(row);
            await Assert.ThrowsAsync<ConflictException>(() => context.SaveChangesAsync());
        }
        await using (var context = factory.Database.CreateContext())
        {
            context.Add(DietPlanNote.Create(Guid.NewGuid(), created.Revision.Id, "Sneaked in", 9, true));
            await Assert.ThrowsAsync<ConflictException>(() => context.SaveChangesAsync());
        }
        await using (var context = factory.Database.CreateContext())
        {
            var version = await context.Set<DietPlanRevision>().SingleAsync(x => x.Id == created.Revision.Id);
            context.Entry(version).Property(x => x.StartDate).CurrentValue = October;
            await Assert.ThrowsAsync<ConflictException>(() => context.SaveChangesAsync());
        }
        await using (var context = factory.Database.CreateContext())
        {
            // The filtered unique index allows one current version per plan.
            context.Add(DietPlanRevision.Create(created.Id, 9, PlanRevisionStatus.Draft, null, null, null, DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        var archived = await coach.GetFromJsonAsync<DietPlanResponse>($"/api/v1/diet-plans/{created.Id}/revisions/1");
        Assert.Equal(archivedRowId, Assert.Single(archived!.Variants[0].Meals[0].FoodItems).Id);
        Assert.Single(archived.Notes);
    }

    [Fact]
    public async Task Workout_versions_and_deliveries_name_the_version_sent()
    {
        var coach = await factory.CreateCoachClientAsync();
        using var admin = factory.CreateAdminClient();
        var category = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/exercise-categories",
            new BilingualReferenceInput("Versioning " + Guid.NewGuid().ToString("N"), null)));
        var squat = await ReadAsync<ExerciseResponse>(await admin.PostAsJsonAsync("/api/v1/training/exercises",
            new ExerciseInput("Squat " + Guid.NewGuid().ToString("N")[..6], null, category.Id, null, null, true, PrimaryMuscleGroupId: TestWorkouts.Quadriceps)));
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Workout versions", null, null, null)));
        var plan = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync("/api/v1/workout-plans",
            Workout(client.Id, squat.Id, 5)));

        var firstDelivery = await ReadAsync<DeliveredPlanResponse>(await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Workout, plan.Id, null, null, "en", DeliveryChannel.Download, null)));
        Assert.Equal((plan.Revision.Id, 1), (firstDelivery.RevisionId!.Value, firstDelivery.VersionNumber!.Value));

        var next = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/workout-plans/{plan.Id}/revisions",
            Workout(client.Id, squat.Id, 3) with { BaseVersionNumber = 1 }));
        Assert.Equal(2, next.Revision.VersionNumber);
        Assert.NotEqual(plan.Days[0].Id, next.Days[0].Id);
        var old = await coach.GetFromJsonAsync<WorkoutPlanResponse>($"/api/v1/workout-plans/{plan.Id}/revisions/1");
        Assert.Equal(5, old!.Days[0].Exercises[0].Sets.Count);
        Assert.True(old.Revision.IsReadOnly);

        var resent = await ReadAsync<DeliveredPlanResponse>(await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Workout, plan.Id, 1, null, "en", DeliveryChannel.Download, null)));
        var latest = await ReadAsync<DeliveredPlanResponse>(await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Workout, plan.Id, null, null, "en", DeliveryChannel.Download, null)));
        Assert.Equal((1, 2), (resent.VersionNumber, latest.VersionNumber));
        await using (var context = factory.Database.CreateContext())
        {
            var snapshot = (await context.Set<DeliveredPlan>().SingleAsync(x => x.Id == resent.Id)).SnapshotJson;
            // Version 1 prescribed five sets; the snapshot keeps them.
            Assert.Equal(5, snapshot.Split("\"WorkoutExerciseId\"").Length - 1);
            Assert.Contains("\"VersionNumber\":1", snapshot);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Workout, plan.Id, 7, null, "en", DeliveryChannel.Download, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Workout, plan.Id, null, Guid.NewGuid(), "en", DeliveryChannel.Download, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Diet, plan.Id, null, null, "en", DeliveryChannel.Download, null))).StatusCode);

        var pdf = await coach.GetAsync($"/api/v1/workout-plans/{plan.Id}/pdf/download?version=1");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Contains("-v1-", pdf.Content.Headers.ContentDisposition!.FileName);
    }

    [Fact]
    public async Task Diet_delivery_takes_a_variant_of_the_chosen_version()
    {
        var coach = await factory.CreateCoachClientAsync();
        var (oats, _) = await FoodsAsync();
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Diet delivery", null, null, null)));
        var plan = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync("/api/v1/diet-plans", Diet(oats.Id, 100) with { ClientId = client.Id }));
        var next = await ReadAsync<DietPlanResponse>(await coach.PostAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions", Edit(plan, oats.Id, 120)));

        var oldVariant = await ReadAsync<DeliveredPlanResponse>(await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Diet, plan.Id, 1, plan.Variants[0].Id, "en", DeliveryChannel.Download, null)));
        Assert.Equal((1, plan.Variants[0].Id), (oldVariant.VersionNumber!.Value, oldVariant.VariantId!.Value));
        // Version 1's variant is not part of the current version.
        Assert.Equal(HttpStatusCode.NotFound, (await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Diet, plan.Id, null, plan.Variants[0].Id, "en", DeliveryChannel.Download, null))).StatusCode);
        var current = await ReadAsync<DeliveredPlanResponse>(await coach.PostAsJsonAsync("/api/v1/plan-deliveries",
            new DeliveredPlanInput(client.Id, DeliveredPlanType.Diet, plan.Id, null, next.Variants[0].Id, "en", DeliveryChannel.Download, null)));
        Assert.Equal(2, current.VersionNumber);
    }

    [Fact]
    public async Task Another_coach_cannot_read_or_save_versions()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachB = await factory.CreateCoachClientAsync();
        var (oats, _) = await FoodsAsync();
        var plan = await ReadAsync<DietPlanResponse>(await coachA.PostAsJsonAsync("/api/v1/diet-plans", Diet(oats.Id, 100)));

        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/diet-plans/{plan.Id}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/diet-plans/{plan.Id}/revisions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.PutAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions/current", Edit(plan, oats.Id, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.PostAsJsonAsync($"/api/v1/diet-plans/{plan.Id}/revisions", Edit(plan, oats.Id, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync($"/api/v1/diet-plans/{plan.Id}/pdf/preview?version=1")).StatusCode);

        using var admin = factory.CreateAdminClient();
        var history = await admin.GetFromJsonAsync<PlanRevisionInfo[]>($"/api/v1/diet-plans/{plan.Id}/revisions");
        Assert.Single(history!);
    }

    private async Task<(FoodResponse Oats, FoodResponse Eggs)> FoodsAsync()
    {
        using var admin = factory.CreateAdminClient();
        var category = await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Versioning " + Guid.NewGuid().ToString("N"), null)));
        var oats = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Oats " + Guid.NewGuid().ToString("N")[..6], null, category.Id, FoodBaseUnit.Gram, 100, 10, 20, 5, null)));
        var eggs = await ReadAsync<FoodResponse>(await admin.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Eggs " + Guid.NewGuid().ToString("N")[..6], null, category.Id, FoodBaseUnit.Gram, 200, 20, 5, 10, null)));
        return (oats, eggs);
    }

    private static DietPlanInput Diet(Guid foodId, decimal grams) => new(
        "Versioned " + Guid.NewGuid().ToString("N")[..6], null, null,
        [new(Guid.NewGuid(), "Drink water", 0, true)],
        [new(Guid.NewGuid(), "Training day", null, 0, true, null,
            [new(Guid.NewGuid(), "Breakfast", null, 0, null, [new(Guid.NewGuid(), foodId, grams, 0, null)])],
            [])]);

    /// <summary>What the editor sends back: the loaded version's content with one quantity changed.</summary>
    private static DietPlanInput Edit(DietPlanResponse plan, Guid foodId, decimal grams) => new(
        plan.NameEn, plan.NameAr, plan.ClientId,
        plan.Notes.Select(x => new DietPlanNoteInput(x.Id, x.Text, x.Order, x.IsActive)).ToArray(),
        plan.Variants.Select(v => new DietPlanVariantInput(v.Id, v.NameEn, v.NameAr, v.Order, v.IsActiveForPdf, v.Notes,
            v.Meals.Select(m => new MealInput(m.Id, m.NameEn, m.NameAr, m.Order, m.Notes,
                m.FoodItems.Select(f => new MealFoodItemInput(f.Id, foodId, grams, f.Order, f.Notes)).ToArray())).ToArray(),
            [])).ToArray(),
        null, plan.Revision.StartDate, plan.Revision.EndDate, plan.Revision.Status, plan.Revision.VersionNumber);

    private static WorkoutPlanInput Workout(Guid clientId, Guid exerciseId, int sets) => new(
        "Program", null, clientId,
        [new(Guid.NewGuid(), "Warm up", 0, true)],
        [new(Guid.NewGuid(), "Legs", null, null, null, 0,
            [new(Guid.NewGuid(), exerciseId, 0, null, TestWorkouts.Sets(sets, 5, null, 180))])],
        StartDate: October);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
