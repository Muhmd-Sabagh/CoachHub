using CoachHub.Domain.Common;

namespace CoachHub.Domain.Billing;

public sealed class Payment : Entity
{
    private Payment() { }
    public Guid InvoiceId { get; private set; }
    public Guid? PaymentAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? Reference { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public IReadOnlyCollection<Refund> Refunds => _refunds;
    private readonly List<Refund> _refunds = [];

    public static Payment Create(Guid invoiceId, Guid? accountId, decimal amount, string? reference, DateTimeOffset at)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice is required.", nameof(invoiceId));
        if (amount <= 0 || amount > 1_000_000m) throw new ArgumentOutOfRangeException(nameof(amount));
        reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (reference?.Length > 200) throw new ArgumentOutOfRangeException(nameof(reference));
        return new() { InvoiceId = invoiceId, PaymentAccountId = accountId, Amount = amount,
            Reference = reference, RecordedAt = at, Status = PaymentStatus.Settled };
    }

    public Refund RecordRefund(decimal amount, string reason, DateTimeOffset at)
    {
        var refunded = _refunds.Sum(x => x.Amount);
        if (amount <= 0 || refunded + amount > Amount) throw new ArgumentOutOfRangeException(nameof(amount));
        var refund = Refund.Create(Id, amount, reason, at);
        _refunds.Add(refund);
        Status = refunded + amount == Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        return refund;
    }
}

public sealed class Refund : Entity
{
    private Refund() { }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    internal static Refund Create(Guid paymentId, decimal amount, string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        reason = reason.Trim();
        if (reason.Length > 500) throw new ArgumentOutOfRangeException(nameof(reason));
        return new() { PaymentId = paymentId, Amount = amount, Reason = reason, RecordedAt = at };
    }
}
