using CoachHub.Application.Coaches;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Coaches;

public sealed class CoachConfiguration : IEntityTypeConfiguration<Coach>
{
    public void Configure(EntityTypeBuilder<Coach> builder)
    {
        builder.ToTable("Coaches");
        builder.HasKey(coach => coach.Id);
        builder.Property(coach => coach.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(coach => coach.IsActive).IsRequired();
        builder.HasIndex(coach => coach.DisplayName);
    }
}

public sealed class CoachRepository(CoachHubDbContext dbContext) : ICoachRepository
{
    public Task<Coach?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<Coach>().SingleOrDefaultAsync(coach => coach.Id == id, cancellationToken);
}
