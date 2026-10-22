using CoachHub.Domain.Billing;

namespace CoachHub.Domain.Clients;

/// <summary>A subscription's payment status from its invoices (§57). Pure.</summary>
public static class SubscriptionPayments
{
    /// <summary>
    /// Voided and draft invoices do not count. Refunded when every counted invoice was refunded; otherwise Paid when
    /// the payments cover the totals, Partially paid when something was paid, else Unpaid. No invoice without any.
    /// </summary>
    public static SubscriptionPaymentStatus Status(IEnumerable<(decimal Total, decimal Paid, InvoiceStatus Status)> invoices)
    {
        var counted = invoices.Where(x => x.Status is not (InvoiceStatus.Voided or InvoiceStatus.Draft)).ToArray();
        if (counted.Length == 0) return SubscriptionPaymentStatus.NoInvoice;
        if (counted.All(x => x.Status == InvoiceStatus.Refunded)) return SubscriptionPaymentStatus.Refunded;
        var total = counted.Sum(x => x.Total);
        var paid = counted.Sum(x => x.Paid);
        if (paid >= total) return SubscriptionPaymentStatus.Paid;
        return paid > 0 ? SubscriptionPaymentStatus.PartiallyPaid : SubscriptionPaymentStatus.Unpaid;
    }
}
