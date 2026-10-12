using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoachHub.IntegrationTests.PlanVersioning;

/// <summary>
/// The hand-written PlanVersioning migration keeps existing plans: each becomes version 1, the old
/// "versions" become its variants, and deliveries point at version 1.
/// </summary>
public sealed class PlanVersioningMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261012143646_ClientProfileAndOnboarding";
    private readonly SqlServerTestDatabase _database = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Existing_plans_become_version_one_with_their_content_and_deliveries()
    {
        Guid coach = Guid.NewGuid(), client = Guid.NewGuid(), diet = Guid.NewGuid(), variant = Guid.NewGuid(),
            meal = Guid.NewGuid(), dietNote = Guid.NewGuid(), workout = Guid.NewGuid(), day = Guid.NewGuid(),
            workoutNote = Guid.NewGuid(), dietDelivery = Guid.NewGuid(), workoutDelivery = Guid.NewGuid(),
            orphanDelivery = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            const string emptyJson = "{}";
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO [Coaches] ([Id], [DisplayName], [IsActive], [CreatedAt]) VALUES ({coach}, N'Legacy coach', 1, SYSDATETIMEOFFSET());
                INSERT INTO [Clients] ([Id], [CoachId], [ClientCode], [Name], [JoinDate], [DietStatus], [WorkoutStatus], [IsActive])
                    VALUES ({client}, {coach}, N'LEG-1', N'Legacy client', '2026-01-01', N'Draft', N'Draft', 1);
                INSERT INTO [DietPlans] ([Id], [CoachId], [NameEn], [ClientId], [CreatedAt]) VALUES ({diet}, {coach}, N'Cut', {client}, SYSDATETIMEOFFSET());
                INSERT INTO [DietPlanVersions] ([Id], [DietPlanId], [NameEn], [Order], [IsActiveForPdf]) VALUES ({variant}, {diet}, N'Training day', 0, 1);
                INSERT INTO [DietPlanMeals] ([Id], [DietPlanVersionId], [NameEn], [Order]) VALUES ({meal}, {variant}, N'Breakfast', 0);
                INSERT INTO [DietPlanNotes] ([Id], [DietPlanId], [Text], [Order], [IsActive]) VALUES ({dietNote}, {diet}, N'Water', 0, 1);
                INSERT INTO [WorkoutPlans] ([Id], [CoachId], [NameEn], [ClientId], [CreatedAt]) VALUES ({workout}, {coach}, N'Strength', {client}, SYSDATETIMEOFFSET());
                INSERT INTO [WorkoutDays] ([Id], [WorkoutPlanId], [NameEn], [Order]) VALUES ({day}, {workout}, N'Push', 0);
                INSERT INTO [WorkoutPlanNotes] ([Id], [WorkoutPlanId], [Text], [Order], [IsActive]) VALUES ({workoutNote}, {workout}, N'Warm up', 0, 1);
                INSERT INTO [DeliveredPlans] ([Id], [CoachId], [ClientId], [PlanType], [PlanId], [VersionId], [PlanNameSnapshot], [Language], [SnapshotJson], [Channel], [DeliveredAt])
                    VALUES ({dietDelivery}, {coach}, {client}, N'Diet', {diet}, {variant}, N'Cut', N'en', {emptyJson}, N'Download', SYSDATETIMEOFFSET()),
                           ({workoutDelivery}, {coach}, {client}, N'Workout', {workout}, {workout}, N'Strength', N'en', {emptyJson}, N'Download', SYSDATETIMEOFFSET()),
                           ({orphanDelivery}, {coach}, {client}, N'Workout', {Guid.NewGuid()}, {Guid.NewGuid()}, N'Deleted plan', N'en', {emptyJson}, N'Download', SYSDATETIMEOFFSET());
                """);
            await context.Database.MigrateAsync();
        }

        await using var migrated = _database.CreateContext();
        var dietVersion = await migrated.Set<DietPlanRevision>().SingleAsync(x => x.PlanId == diet);
        Assert.Equal(1, dietVersion.VersionNumber);
        Assert.Equal(PlanRevisionStatus.Active, dietVersion.Status);
        var keptVariant = await migrated.Set<DietPlanVariant>().SingleAsync(x => x.Id == variant);
        Assert.Equal(dietVersion.Id, keptVariant.RevisionId);
        var keptMeal = await migrated.Set<Meal>().SingleAsync(x => x.Id == meal);
        Assert.Equal((variant, dietVersion.Id), (keptMeal.DietPlanVariantId, keptMeal.RevisionId));
        Assert.Equal(dietVersion.Id, (await migrated.Set<DietPlanNote>().SingleAsync(x => x.Id == dietNote)).RevisionId);

        var workoutVersion = await migrated.Set<WorkoutPlanRevision>().SingleAsync(x => x.PlanId == workout);
        Assert.Equal(workoutVersion.Id, (await migrated.Set<WorkoutDay>().SingleAsync(x => x.Id == day)).RevisionId);
        Assert.Equal(workoutVersion.Id, (await migrated.Set<WorkoutPlanNote>().SingleAsync(x => x.Id == workoutNote)).RevisionId);

        var deliveries = await migrated.Set<DeliveredPlan>().ToDictionaryAsync(x => x.Id);
        Assert.Equal((dietVersion.Id, 1, variant), (deliveries[dietDelivery].RevisionId!.Value, deliveries[dietDelivery].RevisionNumber!.Value, deliveries[dietDelivery].VariantId!.Value));
        Assert.Equal((workoutVersion.Id, 1), (deliveries[workoutDelivery].RevisionId!.Value, deliveries[workoutDelivery].RevisionNumber!.Value));
        Assert.Null(deliveries[workoutDelivery].VariantId);
        Assert.Null(deliveries[orphanDelivery].RevisionId);
        Assert.Null(deliveries[orphanDelivery].RevisionNumber);
    }
}
