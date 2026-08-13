using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class CurrencyService(IReferenceRepository<Currency> repository)
    : ReferenceDataService<Currency, CurrencyInput, CurrencyResponse>(repository)
{
    protected override string ResourceName => "Currency";
    protected override void Validate(CurrencyInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "code", input.Code, 10);
        Required(errors, "name", input.Name, 100);
        Optional(errors, "symbol", input.Symbol, 10);
        ThrowValidation(errors);
    }
    protected override string UniquenessKey(CurrencyInput input) => input.Code;
    protected override Currency Create(CurrencyInput input) =>
        Currency.Create(input.Code, input.Name, input.Symbol, input.IsActive);
    protected override void Update(Currency entity, CurrencyInput input) =>
        entity.Update(input.Code, input.Name, input.Symbol, input.IsActive);
    protected override CurrencyResponse Map(Currency entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive);
}