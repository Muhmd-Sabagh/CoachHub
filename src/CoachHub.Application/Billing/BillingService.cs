using CoachHub.Application.Common.Exceptions;
using CoachHub.Domain.Billing;

namespace CoachHub.Application.Billing;

public sealed class BillingService(IBillingRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvoiceResponse>> ListAsync(Guid? clientId, CancellationToken token) => (await repository.ListAsync(clientId, token)).Select(Map).ToArray();
    public async Task<InvoiceResponse> CreateAsync(InvoiceInput input, CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var sequence = await repository.NextInvoiceSequenceAsync(now.Year, token);
        var invoice = Invoice.Create(input.ClientId, input.SubscriptionId, $"INV-{now.Year}-{sequence:000000}", input.CurrencyId, input.Total, now, input.DueAt);
        await repository.AddInvoiceAsync(invoice, token); return Map(invoice);
    }
    public async Task<InvoiceResponse> PayAsync(Guid invoiceId, PaymentInput input, CancellationToken token)
    {
        var invoice = await RequiredInvoice(invoiceId, token);
        var payment = Payment.Create(invoiceId, input.PaymentAccountId, input.Amount, input.Reference, timeProvider.GetUtcNow());
        invoice.ApplyPayment(input.Amount); await repository.AddPaymentAsync(payment, token); await repository.SaveAsync(token); return Map(invoice);
    }
    public async Task<InvoiceResponse> RefundAsync(Guid paymentId, RefundInput input, CancellationToken token)
    {
        var payment = await repository.FindPaymentAsync(paymentId, token) ?? throw new NotFoundException("Payment", paymentId);
        var invoice = await RequiredInvoice(payment.InvoiceId, token);
        payment.RecordRefund(input.Amount, input.Reason, timeProvider.GetUtcNow()); invoice.ApplyRefund(input.Amount);
        await repository.SaveAsync(token); return Map(invoice);
    }
    public async Task VoidAsync(Guid id, CancellationToken token) { var invoice = await RequiredInvoice(id, token); invoice.Void(); await repository.SaveAsync(token); }
    private async Task<Invoice> RequiredInvoice(Guid id, CancellationToken token) => await repository.FindInvoiceAsync(id, token) ?? throw new NotFoundException("Invoice", id);
    private static InvoiceResponse Map(Invoice x) => new(x.Id, x.ClientId, x.SubscriptionId, x.Number, x.CurrencyId, x.Total, x.Paid, x.Total - x.Paid, x.Status, x.IssuedAt, x.DueAt, x.Payments.Select(p => new PaymentResponse(p.Id, p.PaymentAccountId, p.Amount, p.Status, p.Reference, p.RecordedAt, p.Refunds.Select(r => new RefundResponse(r.Id, r.Amount, r.Reason, r.RecordedAt)).ToArray())).ToArray());
}
