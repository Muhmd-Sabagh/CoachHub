namespace CoachHub.Domain.ReferenceData;

public sealed class Package : ActiveReferenceEntity, IBilingualReference
{
    private Package() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public string? Description { get; private set; }

    public static Package Create(string nameEn, string? nameAr, string? description, bool isActive = true)
    {
        var item = new Package();
        item.Update(nameEn, nameAr, description, isActive);
        return item;
    }

    public void Update(string nameEn, string? nameAr, string? description, bool isActive)
    {
        NameEn = Required(nameEn, 100, nameof(nameEn));
        NameAr = Optional(nameAr, 100, nameof(nameAr));
        Description = Optional(description, 500, nameof(description));
        SetActive(isActive);
    }
}