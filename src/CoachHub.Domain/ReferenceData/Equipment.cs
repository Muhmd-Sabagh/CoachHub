namespace CoachHub.Domain.ReferenceData;

/// <summary>Training equipment (§3 onboarding, §16 exercise library): a platform catalog managed by Administrators.</summary>
public sealed class Equipment : ActiveReferenceEntity, IBilingualReference
{
    private Equipment() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    public static Equipment Create(string nameEn, string? nameAr, bool isActive = true)
    {
        var item = new Equipment();
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
