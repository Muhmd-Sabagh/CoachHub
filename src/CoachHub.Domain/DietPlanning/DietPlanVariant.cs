using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

/// <summary>
/// One ordered tab of a diet plan version (for example "Training day" / "Rest day"). Formerly
/// called DietPlanVersion; "version" now means plan history (D4).
/// </summary>
public sealed class DietPlanVariant : Entity, IPlanRevisionContent
{
    private DietPlanVariant() { }
    public Guid RevisionId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public int Order { get; private set; }
    public bool IsActiveForPdf { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// The same row across plan versions (P22): a new version's row keeps the lineage of the row it came from, so a
    /// client's day choices and logged meals still match after a mid-day new version. A new row starts its own.
    /// </summary>
    public Guid LineageId { get; private set; }

    public static DietPlanVariant Create(
        Guid id, Guid revisionId, string nameEn, string? nameAr,
        int order, bool isActiveForPdf, string? notes, Guid? lineageId = null) => new()
    {
        LineageId = lineageId ?? id,
        Id = DietPlanText.RequiredId(id, nameof(id)),
        RevisionId = DietPlanText.RequiredId(revisionId, nameof(revisionId)),
        NameEn = DietPlanText.Required(nameEn, 200, nameof(nameEn)),
        NameAr = DietPlanText.Optional(nameAr, 200, nameof(nameAr)),
        Order = DietPlanText.Order(order, nameof(order)),
        IsActiveForPdf = isActiveForPdf,
        Notes = DietPlanText.Optional(notes, 2000, nameof(notes))
    };
}
