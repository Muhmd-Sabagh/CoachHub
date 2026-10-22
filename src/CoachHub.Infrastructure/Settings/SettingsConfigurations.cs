using CoachHub.Domain.Media;
using CoachHub.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Settings;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(brand => brand.Id);
        builder.Ignore(brand => brand.IsPlatformBrand);
        builder.Property(brand => brand.BrandName).HasMaxLength(100).IsRequired();
        builder.Property(brand => brand.PrimaryColor).HasMaxLength(7);
        builder.Property(brand => brand.SecondaryColor).HasMaxLength(7);
        builder.Property(brand => brand.LoginHeadline).HasMaxLength(150);
        builder.Property(brand => brand.LoginMessage).HasMaxLength(Brand.MaxTextLength);
        builder.Property(brand => brand.DashboardWelcome).HasMaxLength(Brand.MaxTextLength);
        builder.Property(brand => brand.EmailHeader).HasMaxLength(Brand.MaxTextLength);
        builder.Property(brand => brand.EmailFooter).HasMaxLength(Brand.MaxTextLength);
        builder.Property(brand => brand.Subdomain).HasMaxLength(63);
        builder.Property(brand => brand.CustomDomain).HasMaxLength(253);

        // One brand per coach, and exactly one platform brand: the unfiltered unique index lets
        // SQL Server hold a single NULL CoachId.
        builder.HasIndex(brand => brand.CoachId).IsUnique().HasFilter(null);
        builder.HasIndex(brand => brand.Subdomain).IsUnique().HasFilter("[Subdomain] IS NOT NULL");
        builder.HasIndex(brand => brand.CustomDomain).IsUnique().HasFilter("[CustomDomain] IS NOT NULL");

        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(brand => brand.LogoMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(brand => brand.FaviconMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(brand => brand.DefaultAvatarMediaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.DefaultLanguage).HasMaxLength(5).IsRequired();
        builder.Property(settings => settings.Units).HasConversion<string>().HasMaxLength(20);
        builder.Property(settings => settings.EmailSenderName).HasMaxLength(100);
        builder.Property(settings => settings.DurationPresetList).HasMaxLength(100).IsRequired();
        // The access rules (P16) are read on every client request, so they always load with the settings.
        builder.HasMany(settings => settings.AccessRules).WithOne().HasForeignKey(rule => rule.PlatformSettingsId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(settings => settings.AccessRules).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SubscriptionAccessRuleConfiguration : IEntityTypeConfiguration<SubscriptionAccessRule>
{
    public void Configure(EntityTypeBuilder<SubscriptionAccessRule> builder)
    {
        builder.ToTable("SubscriptionAccessRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(rule => rule.Feature).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(rule => rule.Access).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(rule => new { rule.PlatformSettingsId, rule.Status, rule.Feature }).IsUnique();
    }
}

public sealed class CoachSettingsConfiguration : IEntityTypeConfiguration<CoachSettings>
{
    public void Configure(EntityTypeBuilder<CoachSettings> builder)
    {
        builder.ToTable("CoachSettings");
        builder.HasKey(settings => settings.Id);
        builder.HasIndex(settings => settings.CoachId).IsUnique();
        builder.Ignore(settings => settings.Factors);
        builder.Ignore(settings => settings.Weights);
        builder.Property(settings => settings.SedentaryFactor).HasPrecision(5, 3);
        builder.Property(settings => settings.LightlyActiveFactor).HasPrecision(5, 3);
        builder.Property(settings => settings.ModeratelyActiveFactor).HasPrecision(5, 3);
        builder.Property(settings => settings.VeryActiveFactor).HasPrecision(5, 3);
        builder.Property(settings => settings.ExtraActiveFactor).HasPrecision(5, 3);
        builder.Property(settings => settings.DeficitPercent).HasPrecision(5, 2);
        builder.Property(settings => settings.SurplusPercent).HasPrecision(5, 2);
        builder.Property(settings => settings.ProteinGramsPerKg).HasPrecision(4, 2);
        builder.Property(settings => settings.FatGramsPerKg).HasPrecision(4, 2);
        builder.Property(settings => settings.DefaultMacroMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(settings => settings.ReplacementRule).HasConversion<string>().HasMaxLength(20);
        builder.Property(settings => settings.OneRepMaxFormula).HasConversion<string>().HasMaxLength(20);
        builder.Property(settings => settings.SecondaryMuscleWeight).HasPrecision(3, 2);
        builder.Property(settings => settings.DefaultSleepTargetHours).HasPrecision(4, 2);
        builder.Ignore(settings => settings.DefaultTargets);
    }
}
