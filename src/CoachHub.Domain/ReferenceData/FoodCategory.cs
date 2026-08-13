namespace CoachHub.Domain.ReferenceData;

public sealed class FoodCategory : ActiveReferenceEntity, IBilingualReference
{
    private FoodCategory() { }

    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    public static FoodCategory Create(string nameEn, string? nameAr, bool isActive = true)
    {
        var item = new FoodCategory();
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