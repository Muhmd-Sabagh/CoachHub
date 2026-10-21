using CoachHub.Application.ClientFiles;
using CoachHub.Domain.ClientFiles;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.ClientFiles;

public sealed class ClientFileConfiguration : IEntityTypeConfiguration<ClientFile>
{
    public void Configure(EntityTypeBuilder<ClientFile> builder)
    {
        builder.ToTable("ClientFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(ClientFile.MaximumTitleLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(ClientFile.MaximumDescriptionLength);
        builder.HasIndex(x => new { x.ClientId, x.SharedAt });
        builder.HasIndex(x => x.MediaId).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // The file's content; client deletion removes shared files before the client's media.
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClientFileRepository(CoachHubDbContext dbContext) : IClientFileRepository
{
    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<(ClientFile File, MediaAsset Media)>> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<ClientFile>().AsNoTracking().Where(x => x.ClientId == clientId)
            .Join(dbContext.Set<MediaAsset>(), file => file.MediaId, media => media.Id, (file, media) => new { file, media })
            .ToArrayAsync(cancellationToken);
        return rows.Select(x => (x.file, x.media)).ToArray();
    }

    public async Task<(ClientFile File, MediaAsset Media)?> FindAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        var file = await dbContext.Set<ClientFile>().SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, cancellationToken);
        if (file is null) return null;
        var media = await dbContext.Set<MediaAsset>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == file.MediaId, cancellationToken);
        return media is null ? null : (file, media);
    }

    public void Add(ClientFile file) => dbContext.Set<ClientFile>().Add(file);
    public void Remove(ClientFile file) => dbContext.Set<ClientFile>().Remove(file);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
