using System.Text.Json.Serialization;

namespace CoachHub.Domain.Common;

/// <summary>
/// Lifecycle of one plan version (D4, §59). Each plan has exactly one current version, Draft or
/// Active; every earlier version is Archived and read-only.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanRevisionStatus>))]
public enum PlanRevisionStatus { Draft, Active, Archived }

/// <summary>Which kind of plan a version or event belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanKind>))]
public enum PlanKind { Diet, Workout }

/// <summary>A version became Active: version 1 is a new plan for the client, later ones a new program (§42).</summary>
public sealed record PlanVersionActivated(PlanKind Kind, Guid PlanId, Guid RevisionId, int VersionNumber) : IDomainEvent;

/// <summary>The active version was saved in place (§42 "program updated").</summary>
public sealed record ActivePlanVersionSaved(PlanKind Kind, Guid PlanId, Guid RevisionId) : IDomainEvent;

/// <summary>The plan was given to a client; it notifies them when its current version is already Active.</summary>
public sealed record PlanAssigned(PlanKind Kind, Guid PlanId, Guid ClientId) : IDomainEvent;

/// <summary>A row that belongs to one plan version; archived versions reject changes to it.</summary>
public interface IPlanRevisionContent
{
    Guid RevisionId { get; }
}

/// <summary>
/// One version of a diet or workout plan (D4). "Save in place" updates the current version;
/// "save as new version" archives it and starts the next number. Archived versions never change.
/// </summary>
public abstract class PlanRevision : Entity
{
    public const int MaximumProgramDays = 3660;

    public Guid PlanId { get; private set; }
    public int VersionNumber { get; private set; }
    public PlanRevisionStatus Status { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => Status == PlanRevisionStatus.Archived;

    /// <summary>Diet or workout, for the events this version raises.</summary>
    public abstract PlanKind Kind { get; }

    protected void Initialize(
        Guid planId, int versionNumber, PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate,
        Guid? createdByUserId, DateTimeOffset createdAt)
    {
        if (planId == Guid.Empty) throw new ArgumentException("Plan identifier is required.", nameof(planId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Author identifier cannot be empty.", nameof(createdByUserId));
        PlanId = planId;
        VersionNumber = versionNumber;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Apply(status, startDate, endDate, createdAt);
        if (Status == PlanRevisionStatus.Active) Raise(new PlanVersionActivated(Kind, PlanId, Id, VersionNumber));
    }

    /// <summary>Save in place: the current version's dates and Draft/Active status.</summary>
    public void Update(PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        var wasActive = Status == PlanRevisionStatus.Active;
        Apply(status, startDate, endDate, updatedAt);
        if (Status == PlanRevisionStatus.Active)
            Raise(wasActive ? new ActivePlanVersionSaved(Kind, PlanId, Id) : new PlanVersionActivated(Kind, PlanId, Id, VersionNumber));
    }

    /// <summary>Records that the content was saved in place.</summary>
    public void Touch(DateTimeOffset updatedAt)
    {
        EnsureEditable();
        UpdatedAt = updatedAt;
        if (Status == PlanRevisionStatus.Active) Raise(new ActivePlanVersionSaved(Kind, PlanId, Id));
    }

    /// <summary>Superseded by a new version: from now on this version is read-only.</summary>
    public void Archive(DateTimeOffset archivedAt)
    {
        EnsureEditable();
        Status = PlanRevisionStatus.Archived;
        ArchivedAt = archivedAt;
    }

    public void EnsureEditable()
    {
        if (IsArchived) throw new InvalidOperationException($"Version {VersionNumber} is archived and read-only.");
    }

    /// <summary>Whether the version's program dates cover the day; open ends are unbounded.</summary>
    public bool Covers(DateOnly day) =>
        (StartDate is null || StartDate <= day) && (EndDate is null || day <= EndDate);

    private void Apply(PlanRevisionStatus status, DateOnly? startDate, DateOnly? endDate, DateTimeOffset at)
    {
        if (status is not (PlanRevisionStatus.Draft or PlanRevisionStatus.Active))
            throw new ArgumentException("A current version is either Draft or Active.", nameof(status));
        if (startDate.HasValue && endDate.HasValue)
        {
            if (endDate < startDate) throw new ArgumentException("The end date cannot be before the start date.", nameof(endDate));
            if (endDate.Value.DayNumber - startDate.Value.DayNumber > MaximumProgramDays)
                throw new ArgumentException("A program cannot run longer than ten years.", nameof(endDate));
        }
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = at;
    }
}
