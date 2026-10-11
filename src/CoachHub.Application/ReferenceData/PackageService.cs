using CoachHub.Application.Coaches;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class PackageService(IReferenceRepository<Package> repository, CoachOwnership ownership)
    : ReferenceDataService<Package, PackageInput, PackageResponse>(repository)
{
    protected override string ResourceName => "Package";

    protected override void Validate(PackageInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 100);
        Optional(errors, "nameAr", input.NameAr, 100);
        Optional(errors, "description", input.Description, 500);
        ThrowValidation(errors);
    }

    protected override string UniquenessKey(PackageInput input) => input.NameEn;
    protected override async Task<Guid?> ResolveOwnerAsync(PackageInput input, CancellationToken cancellationToken) =>
        await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
    protected override Guid? OwnerOf(Package entity) => entity.CoachId;
    protected override Package Create(PackageInput input, Guid? ownerCoachId) =>
        Package.Create(ownerCoachId!.Value, input.NameEn, input.NameAr, input.Description, input.IsActive);
    protected override void Update(Package entity, PackageInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.Description, input.IsActive);
    protected override PackageResponse Map(Package entity) =>
        new(entity.Id, entity.CoachId, entity.NameEn, entity.NameAr, entity.Description, entity.IsActive);
}