using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class PaymentAccountService(IReferenceRepository<PaymentAccount> repository)
    : ReferenceDataService<PaymentAccount, PaymentAccountInput, PaymentAccountResponse>(repository)
{
    protected override string ResourceName => "Payment account";
    protected override void Validate(PaymentAccountInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "name", input.Name, 100);
        Optional(errors, "details", input.Details, 500);
        ThrowValidation(errors);
    }
    protected override string UniquenessKey(PaymentAccountInput input) => input.Name;
    protected override PaymentAccount Create(PaymentAccountInput input) =>
        PaymentAccount.Create(input.Name, input.Details, input.IsActive);
    protected override void Update(PaymentAccount entity, PaymentAccountInput input) =>
        entity.Update(input.Name, input.Details, input.IsActive);
    protected override PaymentAccountResponse Map(PaymentAccount entity) =>
        new(entity.Id, entity.Name, entity.Details, entity.IsActive);
}