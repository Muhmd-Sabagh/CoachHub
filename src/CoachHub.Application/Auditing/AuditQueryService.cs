using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;

namespace CoachHub.Application.Auditing;

public sealed class AuditQueryService(IAuditQueryRepository repository)
{
    private static readonly HashSet<string> SortFields =
        ["entitytype", "operation", "actorkind", "actor", "occurredat"];

    public Task<PagedResult<AuditRecord>> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query.Normalize();
        var errors = new Dictionary<string, string[]>();

        if (normalized.OccurredFrom > normalized.OccurredTo)
        {
            errors["occurredAt"] = ["Occurred-from cannot be after occurred-to."];
        }

        if (normalized.SortBy is not null && !SortFields.Contains(normalized.SortBy))
        {
            errors["sortBy"] = ["Unsupported audit sort field."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return repository.ListAsync(normalized, cancellationToken);
    }
}
