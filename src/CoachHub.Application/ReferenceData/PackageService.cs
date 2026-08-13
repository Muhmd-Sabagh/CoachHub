using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class PackageService(IReferenceRepository<Package> repository)
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
    protected override Package Create(PackageInput input) =>
        Package.Create(input.NameEn, input.NameAr, input.Description, input.IsActive);
    protected override void Update(Package entity, PackageInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.Description, input.IsActive);
    protected override PackageResponse Map(Package entity) =>
        new(entity.Id, entity.NameEn, entity.NameAr, entity.Description, entity.IsActive);
}