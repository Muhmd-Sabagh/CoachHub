using CoachHub.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.ReferenceData;

public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages");
        ConfigureBilingual(builder);
        builder.Property(item => item.Description).HasMaxLength(500);
    }

    internal static void ConfigureBilingual<T>(EntityTypeBuilder<T> builder)
        where T : ActiveReferenceEntity, IBilingualReference
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(100);
        builder.HasIndex(item => item.NameEn).IsUnique();
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
        builder.HasIndex(item => item.Name).IsUnique();
        builder.Property(item => item.Details).HasMaxLength(500);
        builder.Property(item => item.IsActive).IsRequired();
    }
}