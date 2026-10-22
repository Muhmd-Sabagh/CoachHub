using CoachHub.Domain.Clients;
using CoachHub.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Clients;

/// <summary>The structured profile (§3): one per client, deleted with the client.</summary>
public sealed class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("ClientProfiles");
        builder.HasKey(profile => profile.Id);
        builder.HasIndex(profile => profile.ClientId).IsUnique();
        builder.HasOne<Client>().WithOne().HasForeignKey<ClientProfile>(profile => profile.ClientId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(profile => profile.Sex).HasConversion<string>().HasMaxLength(10);
        builder.Property(profile => profile.Goal).HasConversion<string>().HasMaxLength(30);
        builder.Property(profile => profile.ActivityLevel).HasConversion<string>().HasMaxLength(30);
        builder.Property(profile => profile.TrainingLocation).HasConversion<string>().HasMaxLength(20);
        // A [Flags] set is stored as its integer value, unlike single-valued enums.
        builder.Property(profile => profile.DietaryRestrictions).IsRequired();
        builder.Property(profile => profile.HeightCm).HasPrecision(5, 1);
        builder.Property(profile => profile.StartingWeightKg).HasPrecision(5, 1);
        builder.Property(profile => profile.StartingBodyFatPercent).HasPrecision(4, 1);
        builder.Property(profile => profile.TrainingPreferences).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.FoodPreferences).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.PreferredFoodsNote).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.DislikedFoodsNote).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.DietaryNotes).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.LifestyleNotes).HasMaxLength(ClientProfile.MaxNoteLength);
        builder.Property(profile => profile.MacroMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(profile => profile.MacroProteinGramsPerKg).HasPrecision(4, 2);
        builder.Property(profile => profile.MacroFatGramsPerKg).HasPrecision(4, 2);
        builder.Property(profile => profile.MacroProteinPercent).HasPrecision(5, 2);
        builder.Property(profile => profile.MacroCarbohydratePercent).HasPrecision(5, 2);
        builder.Property(profile => profile.MacroFatPercent).HasPrecision(5, 2);
        builder.Property(profile => profile.MacroProteinGrams).HasPrecision(6, 1);
        builder.Property(profile => profile.MacroFatGrams).HasPrecision(6, 1);
        builder.Ignore(profile => profile.Macros);
        builder.HasMany(profile => profile.Equipment).WithOne().HasForeignKey(item => item.ClientProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(profile => profile.Foods).WithOne().HasForeignKey(item => item.ClientProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(profile => profile.Equipment).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Foods).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ClientEquipmentConfiguration : IEntityTypeConfiguration<ClientEquipment>
{
    public void Configure(EntityTypeBuilder<ClientEquipment> builder)
    {
        builder.ToTable("ClientEquipment");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.ClientProfileId, item.EquipmentId }).IsUnique();
        // Deleting catalog equipment removes it from profiles.
        builder.HasOne<Equipment>().WithMany().HasForeignKey(item => item.EquipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClientFoodPreferenceConfiguration : IEntityTypeConfiguration<ClientFoodPreference>
{
    public void Configure(EntityTypeBuilder<ClientFoodPreference> builder)
    {
        builder.ToTable("ClientFoodPreferences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(item => new { item.ClientProfileId, item.FoodItemId }).IsUnique();
        builder.HasOne<CoachHub.Domain.Nutrition.FoodItem>().WithMany().HasForeignKey(item => item.FoodItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.ClientCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(client => client.ClientCode).IsUnique();
        builder.Property(client => client.Name).HasMaxLength(255).IsRequired();
        builder.Property(client => client.Phone).HasMaxLength(50);
        builder.Property(client => client.Email).HasMaxLength(255);
        builder.Property(client => client.DietStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(client => client.WorkoutStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(client => client.IsActive).IsRequired();
        builder.Property(client => client.StatusOverride).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(client => client.JoinDate);
        builder.HasMany(client => client.Subscriptions)
            .WithOne()
            .HasForeignKey(subscription => subscription.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(subscription => subscription.SuspensionReason).HasMaxLength(Subscription.MaximumReasonLength);
        builder.HasIndex(subscription => new
        {
            subscription.ClientId,
            subscription.StartDate,
            subscription.EndDate
        });
        builder.HasOne<Package>().WithMany().HasForeignKey(subscription => subscription.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Currency>().WithMany().HasForeignKey(subscription => subscription.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentAccount>().WithMany().HasForeignKey(subscription => subscription.PaymentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(subscription => subscription.Renewals)
            .WithOne()
            .HasForeignKey(renewal => renewal.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SubscriptionRenewalConfiguration : IEntityTypeConfiguration<SubscriptionRenewal>
{
    public void Configure(EntityTypeBuilder<SubscriptionRenewal> builder)
    {
        builder.ToTable("SubscriptionRenewals");
        builder.HasKey(renewal => renewal.Id);
        builder.Property(renewal => renewal.Price).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(renewal => new { renewal.SubscriptionId, renewal.SequenceNumber }).IsUnique();
        builder.HasIndex(renewal => renewal.RecordedAt);
        builder.HasOne<Currency>().WithMany().HasForeignKey(renewal => renewal.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentAccount>().WithMany().HasForeignKey(renewal => renewal.PaymentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
