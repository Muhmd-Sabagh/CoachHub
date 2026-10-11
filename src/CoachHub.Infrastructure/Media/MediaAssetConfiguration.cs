using CoachHub.Domain.Clients;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Media;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("Media");
        builder.HasKey(media => media.Id);
        builder.Property(media => media.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(media => media.StorageKey).IsUnique();
        builder.Property(media => media.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(media => media.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(media => media.SizeBytes).IsRequired();
        builder.Property(media => media.CreatedAt).IsRequired();
        builder.Ignore(media => media.IsPlatformMedia);

        // The coach foreign key and the visibility filter come from TenantModel.
        builder.HasIndex(media => media.ClientId);
        builder.HasOne<Client>().WithMany().HasForeignKey(media => media.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(media => media.OwnerUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(media => media.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
