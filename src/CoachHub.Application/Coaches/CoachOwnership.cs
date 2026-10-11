using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;

namespace CoachHub.Application.Coaches;

/// <summary>
/// Decides which coach owns a new record. Records derived from an existing one (a client's
/// subscription, an invoice's payment) take their coach from that record instead.
/// </summary>
public sealed class CoachOwnership(ICurrentUser currentUser, ICoachRepository coaches)
{
    /// <summary>
    /// A coach always creates records in their own workspace. An administrator must name an
    /// existing, active coach explicitly.
    /// </summary>
    public async Task<Guid> ForNewRecordAsync(Guid? requestedCoachId, CancellationToken cancellationToken)
    {
        if (currentUser.Scope == TenantScope.Coach && currentUser.CoachId is { } own)
        {
            if (requestedCoachId.HasValue && requestedCoachId.Value != own)
            {
                throw new ForbiddenException("Coaches can only create records in their own workspace.");
            }
            return own;
        }

        if (!currentUser.IsAdministrator)
        {
            throw new ForbiddenException("Only coaches and administrators can create coach-owned records.");
        }

        if (requestedCoachId is not { } chosen || chosen == Guid.Empty)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["coachId"] = ["Choose the coach that owns this record."]
            });
        }

        var coach = await coaches.FindAsync(chosen, cancellationToken)
            ?? throw new NotFoundException("Coach", chosen);
        if (!coach.IsActive)
        {
            throw new ConflictException("The selected coach is inactive.");
        }
        return coach.Id;
    }

    /// <summary>Rejects references that cross from one coach's records to another's.</summary>
    public static void EnsureSameCoach(Guid ownerCoachId, Guid referencedCoachId, string field, string message)
    {
        if (ownerCoachId != referencedCoachId)
        {
            throw new ValidationException(new Dictionary<string, string[]> { [field] = [message] });
        }
    }
}
