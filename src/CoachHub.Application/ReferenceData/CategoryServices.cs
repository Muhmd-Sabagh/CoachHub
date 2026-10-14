using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.ReferenceData;

public sealed class FoodCategoryService(IReferenceRepository<FoodCategory> repository)
    : ReferenceDataService<FoodCategory, BilingualReferenceInput, BilingualReferenceResponse>(repository)
{
    protected override string ResourceName => "Food category";
    protected override void Validate(BilingualReferenceInput input) => ValidateNames(input);
    protected override string UniquenessKey(BilingualReferenceInput input) => input.NameEn;
    protected override FoodCategory Create(BilingualReferenceInput input, Guid? ownerCoachId) =>
        FoodCategory.Create(input.NameEn, input.NameAr, input.IsActive);
    protected override void Update(FoodCategory entity, BilingualReferenceInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.IsActive);
    protected override BilingualReferenceResponse Map(FoodCategory entity) =>
        new(entity.Id, entity.NameEn, entity.NameAr, entity.IsActive);

    private static void ValidateNames(BilingualReferenceInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 100);
        Optional(errors, "nameAr", input.NameAr, 100);
        ThrowValidation(errors);
    }
}

public sealed class ExerciseCategoryService(IReferenceRepository<ExerciseCategory> repository)
    : ReferenceDataService<ExerciseCategory, BilingualReferenceInput, BilingualReferenceResponse>(repository)
{
    protected override string ResourceName => "Exercise category";
    protected override void Validate(BilingualReferenceInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 100);
        Optional(errors, "nameAr", input.NameAr, 100);
        ThrowValidation(errors);
    }
    protected override string UniquenessKey(BilingualReferenceInput input) => input.NameEn;
    protected override ExerciseCategory Create(BilingualReferenceInput input, Guid? ownerCoachId) =>
        ExerciseCategory.Create(input.NameEn, input.NameAr, input.IsActive);
    protected override void Update(ExerciseCategory entity, BilingualReferenceInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.IsActive);
    protected override BilingualReferenceResponse Map(ExerciseCategory entity) =>
        new(entity.Id, entity.NameEn, entity.NameAr, entity.IsActive);
}

/// <summary>Muscle groups, a platform catalog (§17, §23).</summary>
public sealed class MuscleGroupService(IReferenceRepository<MuscleGroup> repository)
    : ReferenceDataService<MuscleGroup, BilingualReferenceInput, BilingualReferenceResponse>(repository)
{
    protected override string ResourceName => "Muscle group";
    protected override void Validate(BilingualReferenceInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 100);
        Optional(errors, "nameAr", input.NameAr, 100);
        ThrowValidation(errors);
    }
    protected override string UniquenessKey(BilingualReferenceInput input) => input.NameEn;
    protected override MuscleGroup Create(BilingualReferenceInput input, Guid? ownerCoachId) =>
        MuscleGroup.Create(input.NameEn, input.NameAr, input.IsActive);
    protected override void Update(MuscleGroup entity, BilingualReferenceInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.IsActive);
    protected override BilingualReferenceResponse Map(MuscleGroup entity) =>
        new(entity.Id, entity.NameEn, entity.NameAr, entity.IsActive);
}

/// <summary>Training equipment, a platform catalog (§3, §16).</summary>
public sealed class EquipmentService(IReferenceRepository<Equipment> repository)
    : ReferenceDataService<Equipment, BilingualReferenceInput, BilingualReferenceResponse>(repository)
{
    protected override string ResourceName => "Equipment";
    protected override void Validate(BilingualReferenceInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, "nameEn", input.NameEn, 100);
        Optional(errors, "nameAr", input.NameAr, 100);
        ThrowValidation(errors);
    }
    protected override string UniquenessKey(BilingualReferenceInput input) => input.NameEn;
    protected override Equipment Create(BilingualReferenceInput input, Guid? ownerCoachId) =>
        Equipment.Create(input.NameEn, input.NameAr, input.IsActive);
    protected override void Update(Equipment entity, BilingualReferenceInput input) =>
        entity.Update(input.NameEn, input.NameAr, input.IsActive);
    protected override BilingualReferenceResponse Map(Equipment entity) =>
        new(entity.Id, entity.NameEn, entity.NameAr, entity.IsActive);
}
