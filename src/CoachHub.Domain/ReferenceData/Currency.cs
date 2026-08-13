namespace CoachHub.Domain.ReferenceData;

public sealed class Currency : ActiveReferenceEntity
{
    private Currency() { }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Symbol { get; private set; }

    public static Currency Create(string code, string name, string? symbol, bool isActive = true)
    {
        var item = new Currency();
        item.Update(code, name, symbol, isActive);
        return item;
    }

    public void Update(string code, string name, string? symbol, bool isActive)
    {
        Code = Required(code, 10, nameof(code)).ToUpperInvariant();
        Name = Required(name, 100, nameof(name));
        Symbol = Optional(symbol, 10, nameof(symbol));
        SetActive(isActive);
    }
}