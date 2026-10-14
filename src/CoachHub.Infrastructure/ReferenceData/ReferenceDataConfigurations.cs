using CoachHub.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.ReferenceData;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");
        ConfigureBilingual(builder, uniqueName: false);
        builder.HasIndex(item => new { item.CoachId, item.NameEn }).IsUnique();
        builder.Property(item => item.Description).HasMaxLength(500);
    }

    internal static void ConfigureBilingual<T>(EntityTypeBuilder<T> builder, bool uniqueName = true)
        where T : ActiveReferenceEntity, IBilingualReference
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(100);
        if (uniqueName) builder.HasIndex(item => item.NameEn).IsUnique();
    }
}

public sealed class FoodCategoryConfiguration : IEntityTypeConfiguration<FoodCategory>
{
    public void Configure(EntityTypeBuilder<FoodCategory> builder)
    {
        builder.ToTable("FoodCategories");
        PackageConfiguration.ConfigureBilingual(builder);
    }
}

public sealed class ExerciseCategoryConfiguration : IEntityTypeConfiguration<ExerciseCategory>
{
    public void Configure(EntityTypeBuilder<ExerciseCategory> builder)
    {
        builder.ToTable("ExerciseCategories");
        PackageConfiguration.ConfigureBilingual(builder);
    }
}

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        PackageConfiguration.ConfigureBilingual(builder);
    }
}

/// <summary>Muscle groups (§17, §23), seeded with a standard set that administrators can extend.</summary>
public sealed class MuscleGroupConfiguration : IEntityTypeConfiguration<MuscleGroup>
{
    public void Configure(EntityTypeBuilder<MuscleGroup> builder)
    {
        builder.ToTable("MuscleGroups");
        PackageConfiguration.ConfigureBilingual(builder);
        builder.HasData(MuscleGroupSeed.All.Select(x => new { x.Id, x.NameEn, NameAr = (string?)x.NameAr, IsActive = true }));
    }
}

/// <summary>The muscle groups every installation starts with; their identifiers are fixed.</summary>
public static class MuscleGroupSeed
{
    public static readonly (Guid Id, string NameEn, string NameAr)[] All =
    [
        (new("0e4d6c1a-0001-4c8e-9a1b-5f3e2d1c0b01"), "Chest", "الصدر"),
        (new("0e4d6c1a-0002-4c8e-9a1b-5f3e2d1c0b02"), "Back", "الظهر"),
        (new("0e4d6c1a-0003-4c8e-9a1b-5f3e2d1c0b03"), "Shoulders", "الأكتاف"),
        (new("0e4d6c1a-0004-4c8e-9a1b-5f3e2d1c0b04"), "Biceps", "العضلة ذات الرأسين"),
        (new("0e4d6c1a-0005-4c8e-9a1b-5f3e2d1c0b05"), "Triceps", "العضلة ثلاثية الرؤوس"),
        (new("0e4d6c1a-0006-4c8e-9a1b-5f3e2d1c0b06"), "Forearms", "الساعد"),
        (new("0e4d6c1a-0007-4c8e-9a1b-5f3e2d1c0b07"), "Quadriceps", "الفخذ الأمامي"),
        (new("0e4d6c1a-0008-4c8e-9a1b-5f3e2d1c0b08"), "Hamstrings", "الفخذ الخلفي"),
        (new("0e4d6c1a-0009-4c8e-9a1b-5f3e2d1c0b09"), "Glutes", "الأرداف"),
        (new("0e4d6c1a-000a-4c8e-9a1b-5f3e2d1c0b0a"), "Calves", "السمانة"),
        (new("0e4d6c1a-000b-4c8e-9a1b-5f3e2d1c0b0b"), "Abs", "البطن"),
        (new("0e4d6c1a-000c-4c8e-9a1b-5f3e2d1c0b0c"), "Lower back", "أسفل الظهر"),
        (new("0e4d6c1a-000d-4c8e-9a1b-5f3e2d1c0b0d"), "Traps", "الترابيس"),
        (new("0e4d6c1a-000e-4c8e-9a1b-5f3e2d1c0b0e"), "Adductors", "العضلات المقربة"),
    ];
}

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Symbol).HasMaxLength(10);
        builder.Property(item => item.IsActive).IsRequired();
    }
}

public sealed class PaymentAccountConfiguration : IEntityTypeConfiguration<PaymentAccount>
{
    public void Configure(EntityTypeBuilder<PaymentAccount> builder)
    {
        builder.ToTable("PaymentAccounts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new { item.CoachId, item.Name }).IsUnique();
        builder.Property(item => item.Details).HasMaxLength(500);
        builder.Property(item => item.IsActive).IsRequired();
    }
}