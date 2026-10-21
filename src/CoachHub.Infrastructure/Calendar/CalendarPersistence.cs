using CoachHub.Application.Calendar;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Calendar;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Calendar;

public sealed class TrainingDaysConfiguration : IEntityTypeConfiguration<TrainingDays>
{
    public void Configure(EntityTypeBuilder<TrainingDays> builder)
    {
        builder.ToTable("TrainingDays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Weekdays).HasConversion<string>().HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.ClientId).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents", table => table.HasCheckConstraint("CK_CalendarEvents_Times",
            "[EndTime] IS NULL OR ([StartTime] IS NOT NULL AND [EndTime] > [StartTime])"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(CalendarEvent.MaximumTitleLength).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(CalendarEvent.MaximumNotesLength);
        builder.HasIndex(x => new { x.CoachId, x.Date });
        builder.HasIndex(x => new { x.ClientId, x.Date });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Calendar reads (§45, §46). The tenant filters scope every query to the coach, or to the client.</summary>
public sealed class CalendarRepository(CoachHubDbContext dbContext, IWorkoutTrackingRepository workouts) : ICalendarRepository
{
    public Task<CalendarClient?> FindClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => new CalendarClient(x.Id, x.CoachId, x.Name)).SingleOrDefaultAsync(cancellationToken);

    public Task<TrainingDays?> FindTrainingDaysAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<TrainingDays>().SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

    public async Task<IReadOnlyList<(WorkoutVersion Version, IReadOnlyList<RotationDay> Days)>> WorkoutVersionsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var versions = await workouts.ListWorkoutVersionsAsync(clientId, cancellationToken);
        var revisionIds = versions.Select(x => x.Revision.Id).ToArray();
        var days = (await dbContext.Set<WorkoutDay>().AsNoTracking().Where(x => revisionIds.Contains(x.RevisionId))
            .Select(x => new { x.RevisionId, x.Order, x.NameEn }).ToArrayAsync(cancellationToken)).ToLookup(x => x.RevisionId);
        return versions.Select(version => (version, (IReadOnlyList<RotationDay>)days[version.Revision.Id]
            .OrderBy(x => x.Order).Select(x => new RotationDay(x.Order, x.NameEn)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<LoggedWorkout>> CompletedWorkoutsAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.Set<WorkoutSession>().AsNoTracking()
            .Where(x => x.ClientId == clientId && x.Status == WorkoutSessionStatus.Completed && x.Date >= from && x.Date <= to)
            .Select(x => new LoggedWorkout(x.Id, x.Date, x.NameEn)).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduledClient>> SchedulesAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        var rows = await (
            from schedule in dbContext.Set<CheckInSchedule>().AsNoTracking()
            join client in dbContext.Set<Client>().AsNoTracking() on schedule.ClientId equals client.Id
            where client.IsActive && (clientId == null || client.Id == clientId)
            select new { schedule, client.Name }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new ScheduledClient(x.schedule.ClientId, x.Name, x.schedule)).ToArray();
    }

    public async Task<IReadOnlyList<CheckInOnCalendar>> CheckInsAsync(Guid? clientId, DateOnly first, DateOnly last, CancellationToken cancellationToken) =>
        await (
            from checkIn in dbContext.Set<CheckIn>().AsNoTracking()
            join client in dbContext.Set<Client>().AsNoTracking() on checkIn.ClientId equals client.Id
            where checkIn.DueOn >= first && checkIn.DueOn <= last && (clientId == null || checkIn.ClientId == clientId)
            select new CheckInOnCalendar(checkIn.Id, checkIn.ClientId, client.Name, checkIn.DueOn, checkIn.Status, checkIn.IsLate))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanDates>> PlanDatesAsync(Guid? clientId, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var diets = await (
            from plan in dbContext.Set<DietPlan>().AsNoTracking()
            join client in dbContext.Set<Client>().AsNoTracking() on plan.ClientId equals client.Id
            join revision in dbContext.Set<DietPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where revision.Status == PlanRevisionStatus.Active && (clientId == null || client.Id == clientId) &&
                ((revision.StartDate >= first && revision.StartDate <= last) || (revision.EndDate >= first && revision.EndDate <= last))
            select new PlanDates(PlanKind.Diet, plan.Id, plan.NameEn, client.Id, client.Name, revision.StartDate, revision.EndDate))
            .ToArrayAsync(cancellationToken);
        var workoutPlans = await (
            from plan in dbContext.Set<WorkoutPlan>().AsNoTracking()
            join client in dbContext.Set<Client>().AsNoTracking() on plan.ClientId equals client.Id
            join revision in dbContext.Set<WorkoutPlanRevision>().AsNoTracking() on plan.Id equals revision.PlanId
            where revision.Status == PlanRevisionStatus.Active && (clientId == null || client.Id == clientId) &&
                ((revision.StartDate >= first && revision.StartDate <= last) || (revision.EndDate >= first && revision.EndDate <= last))
            select new PlanDates(PlanKind.Workout, plan.Id, plan.NameEn, client.Id, client.Name, revision.StartDate, revision.EndDate))
            .ToArrayAsync(cancellationToken);
        return [.. diets, .. workoutPlans];
    }

    public async Task<IReadOnlyList<(CalendarEvent Event, string? ClientName)>> EventsAsync(
        Guid? clientId, DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var rows = await (
            from item in dbContext.Set<CalendarEvent>().AsNoTracking()
            where item.Date >= first && item.Date <= last && (clientId == null || item.ClientId == clientId)
            join clientValue in dbContext.Set<Client>().AsNoTracking() on item.ClientId equals clientValue.Id into clients
            from client in clients.DefaultIfEmpty()
            select new { item, Name = client == null ? null : client.Name }).ToArrayAsync(cancellationToken);
        return rows.Select(x => (x.item, (string?)x.Name)).ToArray();
    }

    public Task<CalendarEvent?> FindEventAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<CalendarEvent>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(TrainingDays days) => dbContext.Set<TrainingDays>().Add(days);
    public void Add(CalendarEvent calendarEvent) => dbContext.Set<CalendarEvent>().Add(calendarEvent);
    public void Remove(CalendarEvent calendarEvent) => dbContext.Set<CalendarEvent>().Remove(calendarEvent);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
