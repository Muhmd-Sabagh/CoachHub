using CoachHub.Application.Communications;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Communications;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications"); b.HasKey(x => x.Id); b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Recipient).HasMaxLength(320); b.Property(x => x.Subject).HasMaxLength(200); b.Property(x => x.Body).HasMaxLength(10000); b.Property(x => x.LastError).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.ScheduledAt }); b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        // Event notifications (P14): the in-app list per account, and the occurrence key that makes an event notify once.
        b.Property(x => x.Event).HasConversion<string>().HasMaxLength(30); b.Property(x => x.Link).HasMaxLength(Notification.MaximumLinkLength);
        b.Property(x => x.EventKey).HasMaxLength(Notification.MaximumEventKeyLength);
        b.HasIndex(x => new { x.RecipientUserId, x.Channel, x.ReadAt });
        b.HasIndex(x => new { x.RecipientUserId, x.EventKey }).HasFilter("[EventKey] IS NOT NULL");
    }
}
public sealed class NotificationRepository(CoachHubDbContext db) : INotificationRepository
{
    // The outbox: in-app notifications are each account's own list (InboxRepository), not outgoing messages.
    public async Task<IReadOnlyList<Notification>> ListAsync(CancellationToken token) => await db.Set<Notification>().AsNoTracking().Where(x => x.Channel != NotificationChannel.InApp).OrderByDescending(x => x.ScheduledAt).Take(200).ToArrayAsync(token);
    public async Task<IReadOnlyList<Notification>> DueAsync(DateTimeOffset now, int take, IReadOnlyCollection<NotificationChannel> coachChannels, CancellationToken token) => await db.Set<Notification>().Where(x => (x.Status == NotificationStatus.Pending || x.Status == NotificationStatus.Failed) && x.AttemptCount < 5 && x.ScheduledAt <= now && (x.CoachId == null || x.IsAccountMessage || coachChannels.Contains(x.Channel))).OrderBy(x => x.ScheduledAt).Take(take).ToArrayAsync(token);
    public Task<Notification?> FindAsync(Guid id, CancellationToken token) => db.Set<Notification>().SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task AddAsync(Notification item, CancellationToken token) { db.Add(item); await db.SaveChangesAsync(token); }
    public Task SaveAsync(CancellationToken token) => db.SaveChangesAsync(token);
}
