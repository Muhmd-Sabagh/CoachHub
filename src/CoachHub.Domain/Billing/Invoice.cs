using CoachHub.Domain.Common;

namespace CoachHub.Domain.Billing;

public enum InvoiceStatus { Draft, Issued, PartiallyPaid, Paid, Voided, Refunded }
public enum PaymentStatus { Pending, Settled, Failed, Refunded, PartiallyRefunded }

public sealed class Invoice : Entity
{
    private Invoice() { }
    public Guid ClientId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid CurrencyId { get; private set; }
    public decimal Total { get; private set; }
    public decimal Paid { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public IReadOnlyCollection<Payment> Payments => _payments;
    private readonly List<Payment> _payments = [];

    public static Invoice Create(Guid clientId, Guid? subscriptionId, string number, Guid currencyId,
        decimal total, DateTimeOffset issuedAt, DateTimeOffset? dueAt)
    {
        if (clientId == Guid.Empty || currencyId == Guid.Empty) throw new ArgumentException("Client and currency are required.");
        number = Required(number, 50, nameof(number));
        if (total <= 0 || total > 1_000_000m) throw new ArgumentOutOfRangeException(nameof(total));
        if (dueAt < issuedAt) throw new ArgumentException("Due date cannot precede issue date.", nameof(dueAt));
        return new() { ClientId = clientId, SubscriptionId = subscriptionId, Number = number,
            CurrencyId = currencyId, Total = total, IssuedAt = issuedAt, DueAt = dueAt, Status = InvoiceStatus.Issued };
    }

    public void ApplyPayment(decimal amount)
    {
        if (Status is InvoiceStatus.Voided or InvoiceStatus.Refunded) throw new InvalidOperationException("Invoice cannot accept payments.");
        if (amount <= 0 || Paid + amount > Total) throw new ArgumentOutOfRangeException(nameof(amount));
        Paid += amount;
        Status = Paid == Total ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }

    public void ApplyRefund(decimal amount)
    {
        if (amount <= 0 || amount > Paid) throw new ArgumentOutOfRangeException(nameof(amount));
        Paid -= amount;
        Status = Paid == 0 ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyPaid;
    }

    public void Void()
    {
        if (Paid != 0) throw new InvalidOperationException("A paid invoice cannot be voided.");
        Status = InvoiceStatus.Voided;
    }

    private static string Required(string value, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        value = value.Trim();
        if (value.Length > max) throw new ArgumentOutOfRangeException(name);
        return value;
    }
}
