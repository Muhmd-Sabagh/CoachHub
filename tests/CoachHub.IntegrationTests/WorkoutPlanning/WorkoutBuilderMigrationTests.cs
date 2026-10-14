using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoachHub.IntegrationTests.WorkoutPlanning;

/// <summary>
/// The WorkoutBuilder migration turns free-text prescriptions into typed sets where they parse (D3), keeps the
/// original text in the notes, and leaves existing exercises as platform exercises.
/// </summary>
public sealed class WorkoutBuilderMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261013153050_FoodTracking";
    private readonly SqlServerTestDatabase _database = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Text_prescriptions_become_typed_sets()
    {
        Guid category = Guid.NewGuid(), squat = Guid.NewGuid(), coach = Guid.NewGuid(), plan = Guid.NewGuid(), revision = Guid.NewGuid();
        Guid day = Guid.NewGuid(), parsed = Guid.NewGuid(), loose = Guid.NewGuid(), empty = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO [ExerciseCategories] ([Id], [NameEn], [IsActive]) VALUES ({category}, N'Legs', 1);
                INSERT INTO [Exercises] ([Id], [NameEn], [ExerciseCategoryId], [IsActive]) VALUES ({squat}, N'Squat', {category}, 1);
                INSERT INTO [Coaches] ([Id], [DisplayName], [IsActive], [CreatedAt]) VALUES ({coach}, N'Coach', 1, SYSDATETIMEOFFSET());
                INSERT INTO [WorkoutPlans] ([Id], [CoachId], [NameEn], [CreatedAt]) VALUES ({plan}, {coach}, N'Plan', SYSDATETIMEOFFSET());
                INSERT INTO [WorkoutPlanRevisions] ([Id], [PlanId], [VersionNumber], [Status], [CreatedAt], [UpdatedAt])
                    VALUES ({revision}, {plan}, 1, N'Active', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
                INSERT INTO [WorkoutDays] ([Id], [RevisionId], [NameEn], [Order]) VALUES ({day}, {revision}, N'Legs', 0);
                INSERT INTO [WorkoutExercises] ([Id], [RevisionId], [WorkoutDayId], [ExerciseId], [Order], [Sets], [Repetitions], [Rest], [Tempo], [RpeRir], [Notes])
                VALUES ({parsed}, {revision}, {day}, {squat}, 0, N'3', N'8-12', N'90s', N'3-1-1-0', N'RPE 8', N'Deep'),
                       ({loose}, {revision}, {day}, {squat}, 1, N'3-4', N'AMRAP', N'2 min', NULL, NULL, NULL),
                       ({empty}, {revision}, {day}, {squat}, 2, NULL, NULL, NULL, NULL, NULL, NULL);
                """);
            await context.Database.MigrateAsync();
        }

        await using var migrated = _database.CreateContext();
        var sets = (await migrated.Set<PrescribedSet>().ToListAsync()).ToLookup(x => x.WorkoutExerciseId);
        var exercises = await migrated.Set<WorkoutExercise>().ToDictionaryAsync(x => x.Id);

        Assert.Equal(3, sets[parsed].Count());
        Assert.All(sets[parsed], set => Assert.Equal((8, 12, 90, "3-1-1-0", SetType.Normal),
            (set.RepsMin!.Value, set.RepsMax!.Value, set.RestSeconds!.Value, set.Tempo!.ToString(), set.Type)));
        Assert.Equal("Deep\nPrevious prescription: sets 3, reps 8-12, rest 90s, tempo 3-1-1-0, RPE/RIR RPE 8", exercises[parsed].Notes);
        Assert.Equal(WorkoutBlock.Main, exercises[parsed].Block);

        // Text that does not parse leaves a single, empty set, and stays readable in the notes.
        var unparsed = Assert.Single(sets[loose]);
        Assert.Equal(((int?)null, (int?)null), (unparsed.RepsMin, unparsed.RestSeconds));
        Assert.Equal("Previous prescription: sets 3-4, reps AMRAP, rest 2 min", exercises[loose].Notes);
        Assert.Single(sets[empty]);
        Assert.Null(exercises[empty].Notes);

        var exercise = await migrated.Set<Exercise>().SingleAsync(x => x.Id == squat);
        Assert.Equal(((Guid?)null, (Guid?)null), (exercise.CoachId, exercise.PrimaryMuscleGroupId));
        Assert.Equal(14, await migrated.Set<CoachHub.Domain.ReferenceData.MuscleGroup>().CountAsync());
    }
}
