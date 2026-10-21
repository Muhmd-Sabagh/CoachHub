using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Clients;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.WorkoutTracking;

public sealed class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.ToTable("WorkoutSessions", table =>
        {
            table.HasCheckConstraint("CK_WorkoutSessions_Completed",
                "([Status] = 'Completed' AND [CompletedAt] IS NOT NULL) OR ([Status] = 'InProgress' AND [CompletedAt] IS NULL)");
            table.HasCheckConstraint("CK_WorkoutSessions_PlannedDay",
                "([WorkoutDayId] IS NULL AND [PlanId] IS NULL AND [RevisionId] IS NULL AND [VersionNumber] IS NULL AND [DayOrder] IS NULL) OR " +
                "([WorkoutDayId] IS NOT NULL AND [PlanId] IS NOT NULL AND [RevisionId] IS NOT NULL AND [VersionNumber] IS NOT NULL AND [DayOrder] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(WorkoutSession.MaximumNotesLength);
        builder.Ignore(x => x.IsCompleted);
        builder.Ignore(x => x.IsPlanned);
        builder.HasIndex(x => new { x.ClientId, x.Date });
        // One workout in progress per client.
        builder.HasIndex(x => x.ClientId).IsUnique().HasFilter("[Status] = 'InProgress'")
            .HasDatabaseName("IX_WorkoutSessions_ClientId_InProgress");
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Exercises).WithOne().HasForeignKey(x => x.WorkoutSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Exercises).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
        // PlanId, RevisionId and WorkoutDayId point at plan version rows, which are deleted with their plan. They are
        // references only (no foreign keys); the workout stands on its own.
    }
}

public sealed class LoggedExerciseConfiguration : IEntityTypeConfiguration<LoggedExercise>
{
    public void Configure(EntityTypeBuilder<LoggedExercise> builder)
    {
        builder.ToTable("WorkoutLoggedExercises");
        builder.HasKey(x => x.Id);
        // Added through the workout, so the key is the domain's, not the store's.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Block).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.GroupLabel).HasMaxLength(10);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Ignore(x => x.IsPlanned);
        builder.HasIndex(x => x.ExerciseId);
        // An exercise in a workout log cannot be deleted (deactivate it instead), like one in a plan.
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Sets).WithOne().HasForeignKey(x => x.LoggedExerciseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Sets).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class LoggedSetConfiguration : IEntityTypeConfiguration<LoggedSet>
{
    public void Configure(EntityTypeBuilder<LoggedSet> builder)
    {
        builder.ToTable("WorkoutLoggedSets", table =>
        {
            table.HasCheckConstraint("CK_WorkoutLoggedSets_Values",
                "([Status] = 'Skipped' AND [Reps] IS NULL AND [WeightKg] IS NULL AND [Rir] IS NULL AND [Rpe] IS NULL AND [DurationSeconds] IS NULL) OR " +
                "([Status] = 'Completed' AND ([Reps] IS NOT NULL OR [DurationSeconds] IS NOT NULL))");
            table.HasCheckConstraint("CK_WorkoutLoggedSets_Ranges",
                "([Reps] IS NULL OR [Reps] BETWEEN 0 AND 1000) AND ([WeightKg] IS NULL OR [WeightKg] BETWEEN 0 AND 1000) AND " +
                "([Rir] IS NULL OR [Rir] BETWEEN 0 AND 10) AND ([Rpe] IS NULL OR [Rpe] BETWEEN 1 AND 10) AND " +
                "([DurationSeconds] IS NULL OR [DurationSeconds] BETWEEN 1 AND 36000)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.WeightKg).HasPrecision(6, 2);
        builder.Property(x => x.Rpe).HasPrecision(3, 1);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Ignore(x => x.IsCompleted);
        builder.Ignore(x => x.VolumeKg);
        builder.Ignore(x => x.Values);
    }
}

public sealed class PersonalRecordConfiguration : IEntityTypeConfiguration<PersonalRecord>
{
    public void Configure(EntityTypeBuilder<PersonalRecord> builder)
    {
        builder.ToTable("PersonalRecords", table =>
            table.HasCheckConstraint("CK_PersonalRecords_Value", "[Value] > 0 AND ([PreviousValue] IS NULL OR [PreviousValue] < [Value])"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Value).HasPrecision(10, 2);
        builder.Property(x => x.PreviousValue).HasPrecision(10, 2);
        builder.Property(x => x.WeightKg).HasPrecision(6, 2);
        builder.HasIndex(x => new { x.ClientId, x.ExerciseId, x.Type });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        // WorkoutSessionId and LoggedSetId are references only: records are rebuilt from the workouts, and a foreign
        // key to the workout would give the client's deletion two cascade paths.
    }
}

public sealed class WorkoutTrackingRepository(CoachHubDbContext dbContext) : IWorkoutTrackingRepository
{
    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkoutVersion>> ListWorkoutVersionsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var rows = await (
            from plan in dbContext.Set<WorkoutPlan>().AsNoTracking()
            join revision in dbContext.Set<WorkoutPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where plan.ClientId == clientId
            select new
            {
                plan,
                revision,
                days = dbContext.Set<WorkoutDay>().Count(day => day.RevisionId == revision.Id)
            }).ToListAsync(cancellationToken);
        return rows.Select(x => new WorkoutVersion(x.plan, x.revision, x.days)).ToArray();
    }

    public Task<WorkoutSession?> FindSessionAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
        Sessions(tracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<WorkoutSession?> FindInProgressAsync(Guid clientId, CancellationToken cancellationToken) =>
        Sessions(false).SingleOrDefaultAsync(x => x.ClientId == clientId && x.Status == WorkoutSessionStatus.InProgress, cancellationToken);

    public async Task<IReadOnlyList<WorkoutSession>> ListSessionsAsync(
        Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Sessions(false).Where(x => x.ClientId == clientId && x.Date >= from && x.Date <= to).ToListAsync(cancellationToken);

    public async Task<PagedResult<WorkoutSession>> PageSessionsAsync(
        Guid clientId, WorkoutSessionQuery query, CancellationToken cancellationToken)
    {
        var sessions = Sessions(false).Where(x => x.ClientId == clientId);
        if (query.From is { } from) sessions = sessions.Where(x => x.Date >= from);
        if (query.To is { } to) sessions = sessions.Where(x => x.Date <= to);
        if (query.Status is { } status) sessions = sessions.Where(x => x.Status == status);
        var total = await sessions.LongCountAsync(cancellationToken);
        var items = await sessions.OrderByDescending(x => x.Date).ThenByDescending(x => x.StartedAt)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<WorkoutSession>(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<IReadOnlyList<WorkoutSession>> ListCompletedSessionsWithAsync(
        Guid clientId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken) =>
        await Sessions(false)
            .Where(x => x.ClientId == clientId && x.Status == WorkoutSessionStatus.Completed &&
                x.Exercises.Any(exercise => exerciseIds.Contains(exercise.ExerciseId)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PersonalRecord>> ListRecordsAsync(
        Guid clientId, IReadOnlyCollection<Guid>? exerciseIds, bool tracking, CancellationToken cancellationToken)
    {
        var records = dbContext.Set<PersonalRecord>().Where(x => x.ClientId == clientId);
        if (!tracking) records = records.AsNoTracking();
        if (exerciseIds is not null) records = records.Where(x => exerciseIds.Contains(x.ExerciseId));
        return await records.ToListAsync(cancellationToken);
    }

    // Deliberately cross-tenant: only called for exercises a visible workout or record already references (see the port).
    public async Task<IReadOnlyDictionary<Guid, Exercise>> FindReferencedExercisesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Set<Exercise>().IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

    public Task<Exercise?> FindVisibleExerciseAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Exercise>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, MuscleName>> ListMuscleNamesAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<MuscleGroup>().AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => new MuscleName(x.NameEn, x.NameAr), cancellationToken);

    public async Task AddAsync(WorkoutSession session, CancellationToken cancellationToken)
    {
        dbContext.Add(session);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in dbContext.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToArray())
                entry.State = EntityState.Detached;
            throw new ConflictException("Finish or discard the workout in progress first.");
        }
    }

    public void Remove(WorkoutSession session) => dbContext.Remove(session);
    public void Add(IEnumerable<PersonalRecord> records) => dbContext.AddRange(records);
    public void Remove(IEnumerable<PersonalRecord> records) => dbContext.RemoveRange(records);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<WorkoutSession> Sessions(bool tracking) =>
        tracking ? dbContext.Set<WorkoutSession>() : dbContext.Set<WorkoutSession>().AsNoTracking();
}
