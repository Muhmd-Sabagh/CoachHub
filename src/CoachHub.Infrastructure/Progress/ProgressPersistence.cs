using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Progress;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;
using CoachHub.Domain.Media;
using CoachHub.Domain.Progress;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Progress;

/// <summary>Shared mapping of tracking entries (D3): source, external ID, notes, the client and the date index.</summary>
internal static class ProgressEntryMapping
{
    public static void Configure<T>(EntityTypeBuilder<T> builder, string table, bool onePerDay) where T : ProgressEntry
    {
        builder.ToTable(table);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(ProgressEntry.MaximumExternalIdLength);
        builder.Property(x => x.Notes).HasMaxLength(ProgressEntry.MaximumNotesLength);
        var index = builder.HasIndex(x => new { x.ClientId, x.Date });
        if (onePerDay) index.IsUnique();
        // An imported sample is stored once per provider (future health integrations, §54).
        builder.HasIndex(x => new { x.ClientId, x.Source, x.ExternalId }).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WeightEntryConfiguration : IEntityTypeConfiguration<WeightEntry>
{
    public void Configure(EntityTypeBuilder<WeightEntry> builder)
    {
        ProgressEntryMapping.Configure(builder, "WeightEntries", onePerDay: true);
        builder.Property(x => x.WeightKg).HasPrecision(5, 2);
    }
}

public sealed class BodyFatEntryConfiguration : IEntityTypeConfiguration<BodyFatEntry>
{
    public void Configure(EntityTypeBuilder<BodyFatEntry> builder)
    {
        ProgressEntryMapping.Configure(builder, "BodyFatEntries", onePerDay: true);
        builder.Property(x => x.Percent).HasPrecision(4, 1);
    }
}

public sealed class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        ProgressEntryMapping.Configure(builder, "BodyMeasurements", onePerDay: false);
        builder.Property(x => x.Site).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ValueCm).HasPrecision(4, 1);
        builder.HasIndex(x => new { x.ClientId, x.Date, x.Site }).IsUnique();
    }
}

public sealed class SleepEntryConfiguration : IEntityTypeConfiguration<SleepEntry>
{
    public void Configure(EntityTypeBuilder<SleepEntry> builder)
    {
        ProgressEntryMapping.Configure(builder, "SleepEntries", onePerDay: true);
        builder.Property(x => x.Hours).HasPrecision(4, 2);
        builder.ToTable(table => table.HasCheckConstraint("CK_SleepEntries_Values",
            "[Hours] BETWEEN 0 AND 24 AND ([Quality] IS NULL OR [Quality] BETWEEN 1 AND 5) AND " +
            "(([Bedtime] IS NULL AND [WakeTime] IS NULL) OR ([Bedtime] IS NOT NULL AND [WakeTime] IS NOT NULL))"));
    }
}

public sealed class StepsEntryConfiguration : IEntityTypeConfiguration<StepsEntry>
{
    public void Configure(EntityTypeBuilder<StepsEntry> builder)
    {
        ProgressEntryMapping.Configure(builder, "StepsEntries", onePerDay: true);
        builder.ToTable(table => table.HasCheckConstraint("CK_StepsEntries_Steps", "[Steps] BETWEEN 0 AND 200000"));
    }
}

public sealed class WaterEntryConfiguration : IEntityTypeConfiguration<WaterEntry>
{
    public void Configure(EntityTypeBuilder<WaterEntry> builder)
    {
        ProgressEntryMapping.Configure(builder, "WaterEntries", onePerDay: false);
        builder.ToTable(table => table.HasCheckConstraint("CK_WaterEntries_Amount", "[AmountMl] BETWEEN 1 AND 10000"));
    }
}

public sealed class ProgressPhotoConfiguration : IEntityTypeConfiguration<ProgressPhoto>
{
    public void Configure(EntityTypeBuilder<ProgressPhoto> builder)
    {
        builder.ToTable("ProgressPhotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pose).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(ProgressPhoto.MaximumLabelLength);
        builder.Property(x => x.Notes).HasMaxLength(ProgressEntry.MaximumNotesLength);
        builder.HasIndex(x => new { x.ClientId, x.Date });
        builder.HasIndex(x => x.MediaId).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // The image cannot be deleted while a photo uses it; deleting the photo deletes the image (ProgressService).
        // NoAction: the client's deletion already cascades to photos, and media reaches the client too.
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class LifestyleTargetsConfiguration : IEntityTypeConfiguration<LifestyleTargets>
{
    public void Configure(EntityTypeBuilder<LifestyleTargets> builder)
    {
        builder.ToTable("LifestyleTargets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SleepTargetHours).HasPrecision(4, 2);
        builder.Ignore(x => x.Values);
        builder.HasIndex(x => x.ClientId).IsUnique();
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProgressRepository(CoachHubDbContext dbContext) : IProgressRepository
{
    public Task<Guid?> FindClientCoachIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId).Select(x => (Guid?)x.CoachId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync<T>(Guid clientId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
        where T : ProgressEntry
    {
        var entries = dbContext.Set<T>().AsNoTracking().Where(x => x.ClientId == clientId);
        if (from is { } start) entries = entries.Where(x => x.Date >= start);
        if (to is { } end) entries = entries.Where(x => x.Date <= end);
        return await entries.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> FindOnAsync<T>(Guid clientId, DateOnly date, CancellationToken cancellationToken) where T : ProgressEntry =>
        await dbContext.Set<T>().Where(x => x.ClientId == clientId && x.Date == date).ToListAsync(cancellationToken);

    public Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken) where T : Entity =>
        dbContext.Set<T>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProgressPhoto>> ListPhotosAsync(Guid clientId, PhotoPose? pose, CancellationToken cancellationToken)
    {
        var photos = dbContext.Set<ProgressPhoto>().AsNoTracking().Where(x => x.ClientId == clientId);
        if (pose is { } value) photos = photos.Where(x => x.Pose == value);
        return await photos.ToListAsync(cancellationToken);
    }

    public Task<LifestyleTargets?> FindTargetsAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<LifestyleTargets>().SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

    public void Add(Entity entity) => dbContext.Add(entity);
    public void Remove(Entity entity) => dbContext.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("The entry was changed at the same time. Reload and try again.");
        }
    }
}
