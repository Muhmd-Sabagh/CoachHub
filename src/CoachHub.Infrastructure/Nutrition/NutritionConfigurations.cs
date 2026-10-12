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
        builder.Property(food => food.BaseUnit).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(food => food.PieceNameEn).HasMaxLength(50);
        builder.Property(food => food.PieceNameAr).HasMaxLength(50);
        builder.Property(food => food.ServingSize).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.CaloriesPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.ProteinPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.CarbohydratesPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.FatPer100).HasPrecision(18, 2).IsRequired();
        builder.Property(food => food.FiberPer100).HasPrecision(18, 2);
        builder.Property(food => food.SugarPer100).HasPrecision(18, 2);
        builder.Property(food => food.SodiumMgPer100).HasPrecision(18, 2);
        builder.Property(food => food.IsActive).IsRequired();
        builder.Ignore(food => food.IsPlatform);
        builder.Ignore(food => food.BaseUnitLabel);
        builder.Ignore(food => food.BaseUnitLabelAr);
        builder.HasIndex(food => food.NameEn);
        builder.HasIndex(food => new { food.FoodCategoryId, food.IsActive });
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_FoodItems_PieceName", "[BaseUnit] <> 'Piece' OR [PieceNameEn] IS NOT NULL"));

        builder.HasOne<FoodCategory>()
            .WithMany()
            .HasForeignKey(food => food.FoodCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(food => food.MediaId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(food => food.Units).WithOne().HasForeignKey(unit => unit.FoodItemId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(food => food.Units).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

public sealed class FoodUnitConfiguration : IEntityTypeConfiguration<FoodUnit>
{
    public void Configure(EntityTypeBuilder<FoodUnit> builder)
    {
        builder.ToTable("FoodUnits");
        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.NameEn).HasMaxLength(50).IsRequired();
        builder.Property(unit => unit.NameAr).HasMaxLength(50);
        builder.Property(unit => unit.BaseQuantity).HasPrecision(18, 3).IsRequired();
        builder.HasIndex(unit => new { unit.FoodItemId, unit.Order });
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