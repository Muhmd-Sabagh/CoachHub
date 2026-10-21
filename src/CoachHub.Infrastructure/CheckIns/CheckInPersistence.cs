using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.CheckIns;

public sealed class CheckInScheduleConfiguration : IEntityTypeConfiguration<CheckInSchedule>
{
    public void Configure(EntityTypeBuilder<CheckInSchedule> builder)
    {
        builder.ToTable("CheckInSchedules", table => table.HasCheckConstraint("CK_CheckInSchedules_Interval", "[IntervalWeeks] IN (1, 2, 4)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DueWeekday).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Ignore(x => x.FirstDueOn);
        builder.HasIndex(x => x.ClientId).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("CheckIns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PrivateNotes).HasMaxLength(CheckIn.MaximumTextLength);
        // One check-in per client and due date; a resubmission adds a submission to it.
        builder.HasIndex(x => new { x.ClientId, x.DueOn }).IsUnique();
        builder.HasIndex(x => new { x.CoachId, x.Status, x.SubmittedAt });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // The form stays after a transfer (it is the previous coach's), so the key does not cascade.
        builder.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction);
        // The current submission; client deletion removes check-ins before submissions (ClientRepository).
        builder.HasOne<FormSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.Submissions).WithOne().HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Replies).WithOne().HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Submissions).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.Replies).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CheckInSubmissionConfiguration : IEntityTypeConfiguration<CheckInSubmission>
{
    public void Configure(EntityTypeBuilder<CheckInSubmission> builder)
    {
        builder.ToTable("CheckInSubmissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.FormSubmissionId).IsUnique();
        builder.HasOne<FormSubmission>().WithMany().HasForeignKey(x => x.FormSubmissionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CheckInReplyConfiguration : IEntityTypeConfiguration<CheckInReply>
{
    public void Configure(EntityTypeBuilder<CheckInReply> builder)
    {
        builder.ToTable("CheckInReplies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Text).HasMaxLength(CheckIn.MaximumTextLength).IsRequired();
    }
}

public sealed class CheckInRepository(CoachHubDbContext dbContext) : ICheckInRepository
{
    public Task<Client?> FindClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().SingleOrDefaultAsync(x => x.Id == clientId, cancellationToken);

    public Task<CheckInSchedule?> FindScheduleAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<CheckInSchedule>().SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

    public Task<FormDefinition?> FindFormAsync(Guid definitionId, CancellationToken cancellationToken) =>
        dbContext.Set<FormDefinition>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == definitionId, cancellationToken);

    public Task<bool> HasPublishedVersionAsync(Guid definitionId, CancellationToken cancellationToken) =>
        dbContext.Set<FormVersion>().AnyAsync(x => x.FormDefinitionId == definitionId && x.Status == FormVersionStatus.Published, cancellationToken);

    // Deliberately cross-tenant: only names of forms that visible check-ins and schedules reference (see the port).
    public async Task<IReadOnlyDictionary<Guid, string>> FindFormNamesAsync(IReadOnlyCollection<Guid> definitionIds, CancellationToken cancellationToken) =>
        await dbContext.Set<FormDefinition>().IgnoreQueryFilters().AsNoTracking().Where(x => definitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

    public Task<CheckIn?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken) =>
        (tracking ? dbContext.Set<CheckIn>() : dbContext.Set<CheckIn>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CheckIn>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<CheckIn>().AsNoTracking().Where(x => x.ClientId == clientId).ToListAsync(cancellationToken);

    public async Task<PagedResult<CheckIn>> PageAsync(CheckInQuery query, CancellationToken cancellationToken)
    {
        var checkIns = dbContext.Set<CheckIn>().AsNoTracking();
        if (query.ClientId is { } clientId) checkIns = checkIns.Where(x => x.ClientId == clientId);
        if (query.Status is { } status) checkIns = checkIns.Where(x => x.Status == status);
        var total = await checkIns.LongCountAsync(cancellationToken);
        var items = await checkIns.OrderByDescending(x => x.SubmittedAt)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<CheckIn>(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<PagedResult<Client>> PageDueAsync(CheckInDueQuery query, DateOnly today, CancellationToken cancellationToken)
    {
        var clients = dbContext.Set<Client>().AsNoTracking().Where(x => x.IsActive && x.CheckInDueOn != null);
        clients = query.State switch
        {
            CheckInDueState.Overdue => clients.Where(x => x.CheckInDueOn < today),
            CheckInDueState.Pending => clients.Where(x => x.CheckInDueOn >= today),
            CheckInDueState.Upcoming => clients.Where(x => x.CheckInDueOn > today.AddDays(CheckInService.OpensDaysBefore)),
            _ => clients
        };
        var total = await clients.LongCountAsync(cancellationToken);
        var items = await clients.OrderBy(x => x.CheckInDueOn).ThenBy(x => x.Name)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Client>(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> FindClientNamesAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken) =>
        await dbContext.Set<Client>().AsNoTracking().Where(x => clientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> FindUserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        PlanAuthors.FindNamesAsync(dbContext, userIds, cancellationToken);

    public void Add(CheckIn checkIn) => dbContext.Add(checkIn);
    public void Add(CheckInSchedule schedule) => dbContext.Add(schedule);
    public void Remove(CheckInSchedule schedule) => dbContext.Remove(schedule);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
