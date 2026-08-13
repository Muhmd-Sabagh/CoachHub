using CoachHub.Domain.Clients;
using CoachHub.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Clients;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.ClientCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(client => client.ClientCode).IsUnique();
        builder.Property(client => client.FormCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(client => client.FormCode).IsUnique();
        builder.Property(client => client.Name).HasMaxLength(255).IsRequired();
        builder.Property(client => client.Phone).HasMaxLength(50);
        builder.Property(client => client.Email).HasMaxLength(255);
        builder.Property(client => client.DietStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(client => client.WorkoutStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(client => client.IsActive).IsRequired();
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
    }
}