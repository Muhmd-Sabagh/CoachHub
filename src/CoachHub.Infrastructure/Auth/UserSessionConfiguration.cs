using CoachHub.Domain.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Auth;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(session => session.PreviousTokenHash).HasMaxLength(64);
        builder.Property(session => session.UserAgent).HasMaxLength(UserSession.MaxUserAgentLength);
        builder.Property(session => session.IpAddress).HasMaxLength(UserSession.MaxIpAddressLength);
        builder.Property(session => session.EndReason).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => session.PreviousTokenHash).HasFilter("[PreviousTokenHash] IS NOT NULL");
        builder.HasIndex(session => new { session.UserId, session.RevokedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
