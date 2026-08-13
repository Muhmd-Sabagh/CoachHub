using CoachHub.Domain.Media;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Nutrition;

public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("FoodItems");
        builder.HasKey(food => food.Id);
        builder.Property(food => food.NameEn).HasMaxLength(255).IsRequired();
        builder.Property(food => food.NameAr).HasMaxLength(255);
        builder.Property(food => food.MeasurementUnit).HasMaxLength(50).IsRequired();
        builder.Property(food => food.CaloriesPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.ProteinPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.CarbohydratesPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.FatPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.IsActive).IsRequired();
        builder.HasIndex(food => food.NameEn);
        builder.HasIndex(food => new { food.FoodCategoryId, food.IsActive });

        builder.HasOne<FoodCategory>()
            .WithMany()
            .HasForeignKey(food => food.FoodCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(food => food.MediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LegacyFoodImportRecordConfiguration :
    IEntityTypeConfiguration<LegacyFoodImportRecord>
{
    public void Configure(EntityTypeBuilder<LegacyFoodImportRecord> builder)
    {
        builder.ToTable("LegacyFoodImports");
        builder.HasKey(record => record.Id);
        builder.HasIndex(record => record.LegacyId).IsUnique();
        builder.HasIndex(record => record.FoodItemId).IsUnique();
        builder.Property(record => record.ImportedAt).IsRequired();
        builder.HasOne<FoodItem>()
            .WithOne()
            .HasForeignKey<LegacyFoodImportRecord>(record => record.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}