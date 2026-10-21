using CoachHub.Application.Chat;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Chat;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Chat;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArchivedCoachName).HasMaxLength(Conversation.MaximumCoachNameLength);
        // One open conversation per client; archived ones (earlier coaches) stay alongside.
        builder.HasIndex(x => x.ClientId).IsUnique().HasFilter("[IsArchived] = 0").HasDatabaseName("IX_Conversations_ClientId_Open");
        builder.HasIndex(x => new { x.CoachId, x.LastMessageAt });
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sender).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(ChatMessage.MaximumTextLength);
        builder.HasIndex(x => new { x.ConversationId, x.SentAt });
        // The conversation cascades to its messages; the client key does not, to keep one cascade path.
        builder.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Attachments).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.ToTable("ChatAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FileName).HasMaxLength(ChatAttachment.MaximumFileNameLength).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        // An attached file cannot be deleted while a message holds it; client deletion removes chats first.
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChatRepository(CoachHubDbContext dbContext) : IChatRepository
{
    public Task<ChatClient?> FindClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Client>().Where(x => x.Id == clientId)
            .Select(x => new ChatClient(x.Id, x.CoachId, x.Name)).SingleOrDefaultAsync(cancellationToken);

    public Task<Conversation?> FindOpenAsync(Guid clientId, CancellationToken cancellationToken) =>
        dbContext.Set<Conversation>().SingleOrDefaultAsync(x => x.ClientId == clientId && !x.IsArchived, cancellationToken);

    public Task<Conversation?> FindAsync(Guid clientId, Guid conversationId, CancellationToken cancellationToken) =>
        dbContext.Set<Conversation>().SingleOrDefaultAsync(x => x.Id == conversationId && x.ClientId == clientId, cancellationToken);

    public async Task<IReadOnlyList<Conversation>> ListArchivedAsync(Guid clientId, CancellationToken cancellationToken) =>
        await dbContext.Set<Conversation>().AsNoTracking().Where(x => x.ClientId == clientId && x.IsArchived)
            .OrderByDescending(x => x.ArchivedAt).ToArrayAsync(cancellationToken);

    public async Task<(IReadOnlyList<ChatMessage> Items, bool HasMore)> MessagesAsync(
        Guid conversationId, DateTimeOffset? before, int take, CancellationToken cancellationToken)
    {
        var messages = dbContext.Set<ChatMessage>().AsNoTracking().Where(x => x.ConversationId == conversationId);
        if (before is { } cursor) messages = messages.Where(x => x.SentAt < cursor);
        var page = await messages.OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id).Take(take + 1).ToArrayAsync(cancellationToken);
        return (page.Take(take).ToArray(), page.Length > take);
    }

    public async Task<IReadOnlyList<ChatMessage>> UnreadAsync(Guid conversationId, ChatParticipant reader, CancellationToken cancellationToken) =>
        await dbContext.Set<ChatMessage>().Where(x => x.ConversationId == conversationId && x.Sender != reader && x.ReadAt == null)
            .ToArrayAsync(cancellationToken);

    public Task<int> UnreadCountAsync(Guid conversationId, ChatParticipant reader, CancellationToken cancellationToken) =>
        dbContext.Set<ChatMessage>().CountAsync(x => x.ConversationId == conversationId && x.Sender != reader && x.ReadAt == null, cancellationToken);

    public async Task<PagedResult<ConversationSummary>> ListForCoachAsync(ConversationQuery query, CancellationToken cancellationToken)
    {
        // Open conversations with at least one message; the tenant filters keep them to the coach's clients.
        var conversations = dbContext.Set<Conversation>().AsNoTracking().Where(x => !x.IsArchived && x.LastMessageAt != null);
        var rows = conversations.Join(dbContext.Set<Client>(), c => c.ClientId, client => client.Id, (c, client) => new
        {
            Conversation = c,
            ClientName = client.Name,
            Unread = dbContext.Set<ChatMessage>().Count(m => m.ConversationId == c.Id && m.Sender == ChatParticipant.Client && m.ReadAt == null),
            Last = dbContext.Set<ChatMessage>().Where(m => m.ConversationId == c.Id).OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Text, m.Sender }).FirstOrDefault()
        });
        if (query.UnreadOnly) rows = rows.Where(x => x.Unread > 0);
        if (query.SearchTerm is { } term) rows = rows.Where(x => x.ClientName.Contains(term));
        var ordered = rows.OrderByDescending(x => x.Conversation.LastMessageAt).ThenBy(x => x.Conversation.Id);
        var total = await ordered.LongCountAsync(cancellationToken);
        var page = await ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<ConversationSummary>(page.Select(x => new ConversationSummary(
            x.Conversation.Id, x.Conversation.ClientId, x.ClientName, x.Conversation.LastMessageAt,
            x.Last == null ? null : x.Last.Text ?? "File", x.Last == null ? null : x.Last.Sender, x.Unread)).ToArray(),
            query.PageNumber, query.PageSize, total);
    }

    public Task<ChatMessage?> FindMessageAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<ChatMessage>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(Conversation conversation) => dbContext.Set<Conversation>().Add(conversation);
    public void Add(ChatMessage message) => dbContext.Set<ChatMessage>().Add(message);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
