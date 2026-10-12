using CoachHub.Domain.Clients;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.Nutrition;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.DietPlanning;

public sealed class DietPlanConfiguration : IEntityTypeConfiguration<DietPlan>
{
    public void Configure(EntityTypeBuilder<DietPlan> builder)
    {
        builder.ToTable("DietPlans"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(255).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(255);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.ClientId);
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class DietPlanRevisionConfiguration : IEntityTypeConfiguration<DietPlanRevision>
{
    public void Configure(EntityTypeBuilder<DietPlanRevision> builder)
    {
        builder.ToTable("DietPlanRevisions");
        PlanRevisionMapping.Configure(builder);
        builder.HasOne<DietPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DietPlanVariantConfiguration : IEntityTypeConfiguration<DietPlanVariant>
{
    public void Configure(EntityTypeBuilder<DietPlanVariant> builder)
    {
        builder.ToTable("DietPlanVariants"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200); builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.RevisionId, x.Order }).IsUnique();
        builder.HasOne<DietPlanRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("DietPlanMeals"); builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200); builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.DietPlanVariantId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<DietPlanVariant>().WithMany().HasForeignKey(x => x.DietPlanVariantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MealFoodItemConfiguration : IEntityTypeConfiguration<MealFoodItem>
{
    public void Configure(EntityTypeBuilder<MealFoodItem> builder)
    {
        builder.ToTable("DietPlanMealFoods"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.MealId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<Meal>().WithMany().HasForeignKey(x => x.MealId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.FoodItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DietPlanNoteConfiguration : IEntityTypeConfiguration<DietPlanNote>
{
    public void Configure(EntityTypeBuilder<DietPlanNote> builder)
    {
        builder.ToTable("DietPlanNotes"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.RevisionId, x.Order }).IsUnique();
        builder.HasOne<DietPlanRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DietReplacementGroupConfiguration : IEntityTypeConfiguration<DietReplacementGroup>
{
    public void Configure(EntityTypeBuilder<DietReplacementGroup> builder)
    {
        builder.ToTable("DietReplacementGroups"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.DietPlanVariantId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.HasOne<DietPlanVariant>().WithMany().HasForeignKey(x => x.DietPlanVariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Meal>().WithMany().HasForeignKey(x => x.TargetMealId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<MealFoodItem>().WithMany().HasForeignKey(x => x.TargetMealFoodItemId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class DietReplacementOptionConfiguration : IEntityTypeConfiguration<DietReplacementOption>
{
    public void Configure(EntityTypeBuilder<DietReplacementOption> builder)
    {
        builder.ToTable("DietReplacementOptions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 2); builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.DietReplacementGroupId, x.Order }).IsUnique();
        builder.HasIndex(x => x.RevisionId);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DietReplacementOptions_OneTarget",
            "([ReplacementFoodItemId] IS NOT NULL AND [ReplacementMealId] IS NULL AND [Quantity] IS NOT NULL) OR ([ReplacementFoodItemId] IS NULL AND [ReplacementMealId] IS NOT NULL AND [Quantity] IS NULL)"));
        builder.HasOne<DietReplacementGroup>().WithMany().HasForeignKey(x => x.DietReplacementGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.ReplacementFoodItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Meal>().WithMany().HasForeignKey(x => x.ReplacementMealId).OnDelete(DeleteBehavior.NoAction);
    }
}
