using CoachHub.Domain.Billing;
using CoachHub.Domain.Communications;
using CoachHub.Domain.PlanDelivery;

namespace CoachHub.Domain.Tests.FinalPhase;

public sealed class FinalPhaseDomainTests
{
    [Fact]
    public void Invoice_tracks_partial_and_complete_settlement()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), null, "INV-2026-000001", Guid.NewGuid(), 100m, DateTimeOffset.UtcNow, null);
        invoice.ApplyPayment(40m); Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status); Assert.Equal(40m, invoice.Paid);
        invoice.ApplyPayment(60m); Assert.Equal(InvoiceStatus.Paid, invoice.Status); Assert.Equal(100m, invoice.Paid);
    }

    [Fact]
    public void Payment_cannot_be_refunded_beyond_settlement()
    {
        var payment = Payment.Create(Guid.NewGuid(), null, 50m, null, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => payment.RecordRefund(51m, "Correction", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Notification_records_attempt_and_failure_without_losing_schedule()
    {
        var item = Notification.Schedule(null, NotificationChannel.Email, "client@example.com", "Reminder", "Body", DateTimeOffset.UtcNow);
        item.Start(); item.MarkFailed("Provider unavailable");
        Assert.Equal(NotificationStatus.Failed, item.Status); Assert.Equal(1, item.AttemptCount);
    }

    [Fact]
    public void Delivered_plan_requires_supported_language()
    {
        Assert.Throws<ArgumentException>(() => DeliveredPlan.Create(Guid.NewGuid(), DeliveredPlanType.Diet,
            Guid.NewGuid(), Guid.NewGuid(), "Plan", "fr", "{}", DeliveryChannel.Download, null, DateTimeOffset.UtcNow));
    }
}
