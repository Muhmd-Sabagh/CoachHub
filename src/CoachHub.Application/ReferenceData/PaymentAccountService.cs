using CoachHub.Application.Coaches;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class PaymentAccountService(IReferenceRepository<PaymentAccount> repository, CoachOwnership ownership)
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
    protected override async Task<Guid?> ResolveOwnerAsync(PaymentAccountInput input, CancellationToken cancellationToken) =>
        await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
    protected override Guid? OwnerOf(PaymentAccount entity) => entity.CoachId;
    protected override PaymentAccount Create(PaymentAccountInput input, Guid? ownerCoachId) =>
        PaymentAccount.Create(ownerCoachId!.Value, input.Name, input.Details, input.IsActive);
    protected override void Update(PaymentAccount entity, PaymentAccountInput input) =>
        entity.Update(input.Name, input.Details, input.IsActive);
    protected override PaymentAccountResponse Map(PaymentAccount entity) =>
        new(entity.Id, entity.CoachId, entity.Name, entity.Details, entity.IsActive);
}