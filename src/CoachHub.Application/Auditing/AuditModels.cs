using System.Text.Json;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Auditing;

namespace CoachHub.Application.Auditing;

public sealed record AuditActor(
    AuditActorKind Kind,
    Guid? UserId = null,
    string? DisplayName = null);

public interface IAuditActorAccessor
{
    AuditActor Current { get; }
}

public sealed record AuditQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }

    /// <summary>The affected client (§58).</summary>
    public Guid? ClientId { get; init; }

    /// <summary>The coach whose data changed. Only meaningful to administrators; coaches see their own tenant.</summary>
    public Guid? CoachId { get; init; }
    public AuditOperation? Operation { get; init; }
    public AuditActorKind? ActorKind { get; init; }
    public DateTimeOffset? OccurredFrom { get; init; }
    public DateTimeOffset? OccurredTo { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;

    public AuditQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, PagedRequest.MaximumPageSize),
        SearchTerm = Clean(SearchTerm),
        EntityType = Clean(EntityType),
        SortBy = Clean(SortBy)?.ToLowerInvariant()
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AuditRecord(
    Guid Id,
    string EntityType,
    Guid? EntityId,
    string Operation,
    string ActorKind,
    Guid? ActorUserId,
    string? ActorDisplayName,
    DateTimeOffset OccurredAt,
    Guid? CoachId,
    Guid? ClientId,
    IReadOnlyList<AuditChange> Changes);

/// <summary>One changed property: the previous value (D2) and the new one. Null where not applicable.</summary>
public sealed record AuditChange(string Property, JsonElement? From, JsonElement? To);

public interface IAuditQueryRepository
{
    Task<PagedResult<AuditRecord>> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken);
}
