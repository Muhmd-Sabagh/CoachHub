using CoachHub.Domain.Billing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Tests.Clients;

public sealed class SubscriptionAccessTests
{
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 22);
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static Subscription Sub(Guid clientId, DateOnly start, int months = 1) =>
        Subscription.Create(Coach, clientId, Guid.NewGuid(), start, months, 100, Guid.NewGuid(), null, 0);

    [Fact]
    public void A_subscription_is_pending_active_suspended_or_expired()
    {
        var client = Guid.NewGuid();
        Assert.Equal(SubscriptionStatus.Pending, Sub(client, Today.AddDays(1)).StatusOn(Today));
        var running = Sub(client, Today.AddDays(-5));
        Assert.Equal(SubscriptionStatus.Active, running.StatusOn(Today));
        Assert.Equal(SubscriptionStatus.Expired, running.StatusOn(running.EndDate));
        running.Suspend(" Payment late ", Today, Now);
        Assert.Equal((SubscriptionStatus.Suspended, "Payment late", false), (running.StatusOn(Today), running.SuspensionReason, running.IsActiveOn(Today)));
        // Suspension does not move the end date, and an ended subscription counts as expired.
        Assert.Equal(Today.AddDays(-5).AddMonths(1), running.EndDate);
        Assert.Equal(SubscriptionStatus.Expired, running.StatusOn(running.EndDate));
        running.Resume();
        Assert.Equal((SubscriptionStatus.Active, (DateTimeOffset?)null), (running.StatusOn(Today), running.SuspendedAt));
    }

    [Fact]
    public void Suspending_needs_a_reason_and_a_running_or_future_subscription()
    {
        var client = Guid.NewGuid();
        var running = Sub(client, Today.AddDays(-5));
        Assert.Equal("reason", Assert.Throws<ArgumentException>(() => running.Suspend(" ", Today, Now)).ParamName);
        running.Suspend("Paused", Today, Now);
        Assert.Throws<InvalidOperationException>(() => running.Suspend("Again", Today, Now));
        Assert.Throws<InvalidOperationException>(() => Sub(client, Today.AddMonths(-3)).Suspend("Late", Today, Now));
        Assert.Throws<InvalidOperationException>(() => Sub(client, Today).Resume());
        var future = Sub(client, Today.AddDays(10));
        future.Suspend("Holiday", Today, Now);
        Assert.Equal(SubscriptionStatus.Suspended, future.StatusOn(Today));
    }

    [Fact]
    public void A_client_takes_the_best_status_of_their_subscriptions()
    {
        var client = Client.Create(Coach, "S1B2C3D4", "Client", null, null, Today);
        Assert.Equal(SubscriptionStatus.Inactive, client.GetSubscriptionStatus(Today));
        client.AddSubscription(Sub(client.Id, Today.AddMonths(-3)));
        Assert.Equal(SubscriptionStatus.Expired, client.GetSubscriptionStatus(Today));
        client.AddSubscription(Sub(client.Id, Today.AddDays(3)));
        Assert.Equal(SubscriptionStatus.Pending, client.GetSubscriptionStatus(Today));
        var current = Sub(client.Id, Today.AddDays(-1));
        current.Suspend("Paused", Today, Now);
        client.AddSubscription(current);
        Assert.Equal(SubscriptionStatus.Suspended, client.GetSubscriptionStatus(Today));
        client.AddSubscription(Sub(client.Id, Today.AddDays(-2)));
        Assert.Equal(SubscriptionStatus.Active, client.GetSubscriptionStatus(Today));
    }

    [Theory]
    [InlineData(SubscriptionPaymentStatus.NoInvoice)]
    [InlineData(SubscriptionPaymentStatus.Unpaid, 100, 0, InvoiceStatus.Issued)]
    [InlineData(SubscriptionPaymentStatus.PartiallyPaid, 100, 40, InvoiceStatus.PartiallyPaid)]
    [InlineData(SubscriptionPaymentStatus.Paid, 100, 100, InvoiceStatus.Paid)]
    [InlineData(SubscriptionPaymentStatus.Refunded, 100, 0, InvoiceStatus.Refunded)]
    [InlineData(SubscriptionPaymentStatus.NoInvoice, 100, 0, InvoiceStatus.Voided)]
    public void Payment_status_comes_from_the_invoices(SubscriptionPaymentStatus expected, double total = 0, double paid = 0, InvoiceStatus status = InvoiceStatus.Issued)
    {
        (decimal, decimal, InvoiceStatus)[] invoices = total == 0 ? [] : [((decimal)total, (decimal)paid, status)];
        Assert.Equal(expected, SubscriptionPayments.Status(invoices));
    }

    [Fact]
    public void Two_invoices_are_paid_only_when_both_are_covered()
    {
        Assert.Equal(SubscriptionPaymentStatus.PartiallyPaid, SubscriptionPayments.Status([(100m, 100m, InvoiceStatus.Paid), (50m, 0m, InvoiceStatus.Issued)]));
        Assert.Equal(SubscriptionPaymentStatus.Paid, SubscriptionPayments.Status([(100m, 100m, InvoiceStatus.Paid), (50m, 50m, InvoiceStatus.Paid)]));
    }

    [Fact]
    public void Access_rules_default_to_read_only_for_expired_and_suspended()
    {
        var settings = PlatformSettings.CreateDefault(Now);
        Assert.Equal(FeatureAccess.Full, settings.AccessFor(SubscriptionStatus.Active, ClientFeature.Chat));
        Assert.Equal(FeatureAccess.ReadOnly, settings.AccessFor(SubscriptionStatus.Expired, ClientFeature.Nutrition));
        Assert.Equal(FeatureAccess.ReadOnly, settings.AccessFor(SubscriptionStatus.Suspended, ClientFeature.Chat));
        Assert.Equal(FeatureAccess.Full, settings.AccessFor(SubscriptionStatus.Pending, ClientFeature.Training));
        Assert.Equal(FeatureAccess.Full, settings.AccessFor(SubscriptionStatus.Inactive, ClientFeature.Files));
        Assert.Equal([1, 3, 6, 12], settings.DurationPresets);
    }

    [Fact]
    public void The_administrator_sets_rules_and_presets()
    {
        var settings = PlatformSettings.CreateDefault(Now);
        settings.SetSubscriptionRules([12, 1, 6], [(SubscriptionStatus.Expired, ClientFeature.Chat, FeatureAccess.Blocked),
            (SubscriptionStatus.Inactive, ClientFeature.Nutrition, FeatureAccess.ReadOnly)], Now);
        Assert.Equal([1, 6, 12], settings.DurationPresets);
        Assert.Equal(FeatureAccess.Blocked, settings.AccessFor(SubscriptionStatus.Expired, ClientFeature.Chat));
        Assert.Equal(FeatureAccess.ReadOnly, settings.AccessFor(SubscriptionStatus.Inactive, ClientFeature.Nutrition));
        Assert.Equal(FeatureAccess.ReadOnly, settings.AccessFor(SubscriptionStatus.Expired, ClientFeature.Files));

        // Leaving a rule out returns it to its default.
        settings.SetSubscriptionRules([1], [], Now);
        Assert.Equal(FeatureAccess.ReadOnly, settings.AccessFor(SubscriptionStatus.Expired, ClientFeature.Chat));
        Assert.Empty(settings.AccessRules);

        Assert.Throws<ArgumentException>(() => settings.SetSubscriptionRules([1], [(SubscriptionStatus.Active, ClientFeature.Chat, FeatureAccess.Blocked)], Now));
        Assert.Throws<ArgumentException>(() => settings.SetSubscriptionRules([], [], Now));
        Assert.Throws<ArgumentException>(() => settings.SetSubscriptionRules([1, 1], [], Now));
        Assert.Throws<ArgumentException>(() => settings.SetSubscriptionRules([121], [], Now));
        Assert.Throws<ArgumentException>(() => settings.SetSubscriptionRules([1], [(SubscriptionStatus.Expired, ClientFeature.Chat, FeatureAccess.Blocked),
            (SubscriptionStatus.Expired, ClientFeature.Chat, FeatureAccess.Full)], Now));
    }
}
