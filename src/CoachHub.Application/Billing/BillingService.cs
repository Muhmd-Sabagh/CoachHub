using CoachHub.Application.Clients;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Billing;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Billing;

public sealed class BillingService(
    IBillingRepository repository,
    IClientRepository clients,
    IReferenceRepository<PaymentAccount> paymentAccounts,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<InvoiceResponse>> ListAsync(Guid? clientId, CancellationToken token) => (await repository.ListAsync(clientId, token)).Select(Map).ToArray();
    public async Task<InvoiceResponse> CreateAsync(InvoiceInput input, CancellationToken token)
    {
        var client = await clients.FindAsync(input.ClientId, token) ?? throw new NotFoundException("Client", input.ClientId);
        if (input.SubscriptionId is { } subscriptionId)
        {
            var subscription = await clients.FindSubscriptionAsync(subscriptionId, token);
            if (subscription is null || subscription.ClientId != client.Id)
                throw new NotFoundException("Subscription", subscriptionId);
            CoachOwnership.EnsureSameCoach(client.CoachId, subscription.CoachId, "subscriptionId", "The subscription was sold by another coach.");
        }
        var now = timeProvider.GetUtcNow();
        var sequence = await repository.NextInvoiceSequenceAsync(client.CoachId, now.Year, token);
        var invoice = Invoice.Create(client.CoachId, input.ClientId, input.SubscriptionId, $"INV-{now.Year}-{sequence:000000}", input.CurrencyId, input.Total, now, input.DueAt);
        await repository.AddInvoiceAsync(invoice, token); return Map(invoice);
    }
    public async Task<InvoiceResponse> PayAsync(Guid invoiceId, PaymentInput input, CancellationToken token)
    {
        var invoice = await RequiredInvoice(invoiceId, token);
        if (input.PaymentAccountId is { } accountId &&
            (await paymentAccounts.FindAsync(accountId, token))?.CoachId != invoice.CoachId)
            throw new ValidationException(new Dictionary<string, string[]> { ["paymentAccountId"] = ["The selected payment account does not exist for this coach."] });
        var payment = Payment.Create(invoice.CoachId, invoiceId, input.PaymentAccountId, input.Amount, input.Reference, timeProvider.GetUtcNow());
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
