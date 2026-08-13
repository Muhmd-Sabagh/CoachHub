using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Auditing;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Auditing;

public sealed class AuditQueryRepository(CoachHubDbContext dbContext) : IAuditQueryRepository
{
    public async Task<PagedResult<AuditRecord>> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        var rows = dbContext.Set<AuditEntry>().AsNoTracking();

        if (query.SearchTerm is not null)
            rows = rows.Where(entry => entry.EntityType.Contains(query.SearchTerm) ||
                (entry.ActorDisplayName != null && entry.ActorDisplayName.Contains(query.SearchTerm)));
        if (query.EntityType is not null)
            rows = rows.Where(entry => entry.EntityType == query.EntityType);
        if (query.EntityId.HasValue)
            rows = rows.Where(entry => entry.EntityId == query.EntityId);
        if (query.Operation.HasValue)
            rows = rows.Where(entry => entry.Operation == query.Operation);
        if (query.ActorKind.HasValue)
            rows = rows.Where(entry => entry.ActorKind == query.ActorKind);
        if (query.OccurredFrom.HasValue)
            rows = rows.Where(entry => entry.OccurredAt >= query.OccurredFrom.Value);
        if (query.OccurredTo.HasValue)
            rows = rows.Where(entry => entry.OccurredAt <= query.OccurredTo.Value);

        var totalCount = await rows.LongCountAsync(cancellationToken);
        rows = ApplySort(rows, query);
        var entities = await rows
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var page = entities.Select(entry => new AuditRecord(
            entry.Id,
            entry.EntityType,
            entry.EntityId,
            entry.Operation.ToString(),
            entry.ActorKind.ToString(),
            entry.ActorUserId,
            entry.ActorDisplayName,
            entry.OccurredAt)).ToArray();

        return new(page, query.PageNumber, query.PageSize, totalCount);
    }

    private static IQueryable<AuditEntry> ApplySort(
        IQueryable<AuditEntry> rows,
        AuditQuery query)
    {
        var descending = query.SortBy is null || query.SortDescending;
        IOrderedQueryable<AuditEntry> ordered = query.SortBy switch
        {
            "entitytype" => descending
                ? rows.OrderByDescending(x => x.EntityType)
                : rows.OrderBy(x => x.EntityType),
            "operation" => descending
                ? rows.OrderByDescending(x => x.Operation)
                : rows.OrderBy(x => x.Operation),
            "actorkind" => descending
                ? rows.OrderByDescending(x => x.ActorKind)
                : rows.OrderBy(x => x.ActorKind),
            "actor" => descending
                ? rows.OrderByDescending(x => x.ActorDisplayName)
                : rows.OrderBy(x => x.ActorDisplayName),
            _ => descending
                ? rows.OrderByDescending(x => x.OccurredAt)
                : rows.OrderBy(x => x.OccurredAt)
        };
        return ordered.ThenByDescending(x => x.Id);
    }
}
