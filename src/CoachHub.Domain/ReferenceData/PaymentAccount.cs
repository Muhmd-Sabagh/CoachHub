namespace CoachHub.Domain.ReferenceData;

public sealed class PaymentAccount : ActiveReferenceEntity
{
    private PaymentAccount() { }

    public string Name { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    public static PaymentAccount Create(string name, string? details, bool isActive = true)
    {
        var item = new PaymentAccount();
        item.Update(name, details, isActive);
        return item;
    }

    public void Update(string name, string? details, bool isActive)
    {
        Name = Required(name, 100, nameof(name));
        Details = Optional(details, 500, nameof(details));
        SetActive(isActive);
    }
}