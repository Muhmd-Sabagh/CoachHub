using CoachHub.Domain.Common;

namespace CoachHub.Domain.ReferenceData;

public sealed class PaymentAccount : ActiveReferenceEntity, ITenantOwned
{
    private PaymentAccount() { }

    public Guid CoachId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Details { get; private set; }

    public static PaymentAccount Create(Guid coachId, string name, string? details, bool isActive = true)
    {
        var item = new PaymentAccount { CoachId = TenantKey.Required(coachId) };
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