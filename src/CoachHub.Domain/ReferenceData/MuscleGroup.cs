namespace CoachHub.Domain.ReferenceData;

/// <summary>
/// A muscle group (§17, §23): a platform catalog managed by Administrators. Exercises name one primary and
/// any secondary groups; training analytics count volume per group (P10).
/// </summary>
public sealed class MuscleGroup : ActiveReferenceEntity, IBilingualReference
{
    private MuscleGroup() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    public static MuscleGroup Create(string nameEn, string? nameAr, bool isActive = true)
    {
        var item = new MuscleGroup();
        item.Update(nameEn, nameAr, isActive);
        return item;
    }

    public void Update(string nameEn, string? nameAr, bool isActive)
    {
        NameEn = Required(nameEn, 100, nameof(nameEn));
        NameAr = Optional(nameAr, 100, nameof(nameAr));
        SetActive(isActive);
    }
}
