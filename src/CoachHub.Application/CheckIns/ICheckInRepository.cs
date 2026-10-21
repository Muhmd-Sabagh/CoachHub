using CoachHub.Application.Common.Models;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.CheckIns;

public interface ICheckInRepository
{
    /// <summary>A visible client (tracked, for its check-in state).</summary>
    Task<Client?> FindClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<CheckInSchedule?> FindScheduleAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>A visible form definition.</summary>
    Task<FormDefinition?> FindFormAsync(Guid definitionId, CancellationToken cancellationToken);
    Task<bool> HasPublishedVersionAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Form names by ID, whoever owns them: check-ins keep their form after a transfer, and a visible check-in may
    /// name the previous coach's form.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> FindFormNamesAsync(IReadOnlyCollection<Guid> definitionIds, CancellationToken cancellationToken);

    Task<CheckIn?> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckIn>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<PagedResult<CheckIn>> PageAsync(CheckInQuery query, CancellationToken cancellationToken);

    /// <summary>Scheduled clients by next due date: overdue (before <paramref name="today"/>) or pending (from today).</summary>
    Task<PagedResult<Client>> PageDueAsync(CheckInDueQuery query, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> FindClientNamesAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> FindUserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    void Add(CheckIn checkIn);
    void Add(CheckInSchedule schedule);
    void Remove(CheckInSchedule schedule);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
