using CoachHub.Domain.Billing;

namespace CoachHub.Application.Billing;

public sealed record InvoiceInput(Guid ClientId, Guid? SubscriptionId, Guid CurrencyId, decimal Total, DateTimeOffset? DueAt);
public sealed record PaymentInput(Guid? PaymentAccountId, decimal Amount, string? Reference);
public sealed record RefundInput(decimal Amount, string Reason);
public sealed record RefundResponse(Guid Id, decimal Amount, string Reason, DateTimeOffset RecordedAt);
public sealed record PaymentResponse(Guid Id, Guid? PaymentAccountId, decimal Amount, PaymentStatus Status, string? Reference, DateTimeOffset RecordedAt, IReadOnlyList<RefundResponse> Refunds);
public sealed record InvoiceResponse(Guid Id, Guid ClientId, Guid? SubscriptionId, string Number, Guid CurrencyId, decimal Total, decimal Paid, decimal Balance, InvoiceStatus Status, DateTimeOffset IssuedAt, DateTimeOffset? DueAt, IReadOnlyList<PaymentResponse> Payments);

public interface IBillingRepository
{
    Task<IReadOnlyList<Invoice>> ListAsync(Guid? clientId, CancellationToken token);
    Task<Invoice?> FindInvoiceAsync(Guid id, CancellationToken token);
    Task<Payment?> FindPaymentAsync(Guid id, CancellationToken token);
    Task AddInvoiceAsync(Invoice invoice, CancellationToken token);
    Task AddPaymentAsync(Payment payment, CancellationToken token);
    Task SaveAsync(CancellationToken token);
    Task<int> NextInvoiceSequenceAsync(int year, CancellationToken token);
}
