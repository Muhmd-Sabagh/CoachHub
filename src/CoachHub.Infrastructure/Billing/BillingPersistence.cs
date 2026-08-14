using CoachHub.Application.Billing;
using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.ReferenceData;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoachHub.Infrastructure.Billing;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices"); b.HasKey(x => x.Id); b.Property(x => x.Number).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Total).HasPrecision(18, 2); b.Property(x => x.Paid).HasPrecision(18, 2); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.ClientId, x.IssuedAt }); b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Reference).HasMaxLength(200); b.HasIndex(x => x.RecordedAt); b.HasOne<PaymentAccount>().WithMany().HasForeignKey(x => x.PaymentAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Refunds).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b) { b.ToTable("Refunds"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Reason).HasMaxLength(500); b.HasIndex(x => x.RecordedAt); }
}

public sealed class BillingRepository(CoachHubDbContext db) : IBillingRepository
{
    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid? clientId, CancellationToken token)
    {
        var query = db.Set<Invoice>().AsNoTracking().Include(x => x.Payments).ThenInclude(x => x.Refunds).AsQueryable();
        if (clientId.HasValue) query = query.Where(x => x.ClientId == clientId);
        return await query.OrderByDescending(x => x.IssuedAt).Take(200).ToArrayAsync(token);
    }
    public Task<Invoice?> FindInvoiceAsync(Guid id, CancellationToken token) => db.Set<Invoice>().Include(x => x.Payments).ThenInclude(x => x.Refunds).SingleOrDefaultAsync(x => x.Id == id, token);
    public Task<Payment?> FindPaymentAsync(Guid id, CancellationToken token) => db.Set<Payment>().Include(x => x.Refunds).SingleOrDefaultAsync(x => x.Id == id, token);
    public async Task AddInvoiceAsync(Invoice invoice, CancellationToken token) { db.Add(invoice); await db.SaveChangesAsync(token); }
    public async Task AddPaymentAsync(Payment payment, CancellationToken token) { db.Add(payment); await db.SaveChangesAsync(token); }
    public Task SaveAsync(CancellationToken token) => db.SaveChangesAsync(token);
    public async Task<int> NextInvoiceSequenceAsync(int year, CancellationToken token) => await db.Set<Invoice>().CountAsync(x => x.IssuedAt.Year == year, token) + 1;
}
