using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Clients;
using CoachHub.Domain.ReferenceData;

namespace CoachHub.Application.Clients;

public sealed class SubscriptionService(
    IClientRepository repository,
    IReferenceRepository<Package> packages,
    IReferenceRepository<Currency> currencies,
    IReferenceRepository<PaymentAccount> paymentAccounts,
    TimeProvider timeProvider)
{
    public async Task<SubscriptionResponse> CreateAsync(
        Guid clientId,
        SubscriptionInput input,
        CancellationToken cancellationToken)
    {
        var client = await repository.FindAsync(clientId, cancellationToken)
            ?? throw new NotFoundException("Client", clientId);
        Validate(input);
        await ValidateReferencesAsync(
            client.CoachId,
            input.PackageId,
            input.CurrencyId,
            input.PaymentAccountId,
            cancellationToken);
        var subscription = Subscription.Create(
            client.CoachId,
            clientId,
            input.PackageId,
            input.StartDate,
            input.DurationMonths,
            input.Price,
            input.CurrencyId,
            input.PaymentAccountId,
            input.RenewalCount);
        await repository.AddSubscriptionAsync(subscription, cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionResponse> UpdateAsync(
        Guid clientId,
        Guid id,
        SubscriptionInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
        await ValidateReferencesAsync(
            subscription.CoachId,
            input.PackageId,
            input.CurrencyId,
            input.PaymentAccountId,
            cancellationToken);
        if (subscription.Renewals.Count > 0)
        {
            throw new ConflictException(
                "A subscription with renewal history cannot be edited.");
        }
        subscription.Update(
            input.PackageId,
            input.StartDate,
            input.DurationMonths,
            input.Price,
            input.CurrencyId,
            input.PaymentAccountId,
            input.RenewalCount);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionResponse> RenewAsync(
        Guid clientId,
        Guid id,
        SubscriptionRenewalInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
        await ValidateReferencesAsync(
            subscription.CoachId, null, input.CurrencyId, input.PaymentAccountId, cancellationToken);
        if (subscription.RenewalCount >= 1000)
        {
            throw new ConflictException("The subscription renewal limit has been reached.");
        }
        var renewal = subscription.Renew(
            input.DurationMonths,
            input.Price,
            input.CurrencyId,
            input.PaymentAccountId,
            timeProvider.GetUtcNow());
        await repository.AddRenewalAsync(renewal, cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    /// <summary>Pauses the subscription (§57 Suspended); its end date does not move, and no data is touched.</summary>
    public async Task<SubscriptionResponse> SuspendAsync(Guid clientId, Guid id, SubscriptionSuspendInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
        try
        {
            DomainValidation.Run(() => subscription.Suspend(input.Reason, Today(), timeProvider.GetUtcNow()), "reason");
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(exception.Message);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionResponse> ResumeAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
        try
        {
            subscription.Resume();
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(exception.Message);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(subscription, cancellationToken);
    }

    private async Task<SubscriptionResponse> MapAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var payments = await repository.SubscriptionPaymentsAsync([subscription.Id], cancellationToken);
        return Map(subscription, Today(), payments.GetValueOrDefault(subscription.Id, SubscriptionPaymentStatus.NoInvoice));
    }

    public async Task DeleteAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
        if (subscription.Renewals.Count > 0)
        {
            throw new ConflictException(
                "A subscription with renewal history cannot be deleted.");
        }
        await repository.DeleteSubscriptionAsync(subscription, cancellationToken);
    }

    internal static SubscriptionResponse Map(Subscription subscription, DateOnly today, SubscriptionPaymentStatus payment) => new(
        subscription.Id,
        subscription.ClientId,
        subscription.PackageId,
        subscription.StartDate,
        subscription.EndDate,
        subscription.DurationMonths,
        subscription.Price,
        subscription.CurrencyId,
        subscription.PaymentAccountId,
        subscription.RenewalCount,
        subscription.IsActiveOn(today),
        subscription.Renewals
            .OrderByDescending(renewal => renewal.SequenceNumber)
            .Select(renewal => new SubscriptionRenewalResponse(
                renewal.Id,
                renewal.SequenceNumber,
                renewal.PreviousEndDate,
                renewal.NewEndDate,
                renewal.DurationMonths,
                renewal.Price,
                renewal.CurrencyId,
                renewal.PaymentAccountId,
                renewal.RecordedAt))
            .ToArray(),
        subscription.StatusOn(today),
        payment,
        subscription.SuspendedAt,
        subscription.SuspensionReason);

    private async Task<Subscription> FindRequiredAsync(
        Guid clientId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var subscription = await repository.FindSubscriptionAsync(id, cancellationToken);
        return subscription is not null && subscription.ClientId == clientId
            ? subscription
            : throw new NotFoundException("Subscription", id);
    }

    /// <summary>Packages and payment accounts must belong to the coach who sells the subscription.</summary>
    private async Task ValidateReferencesAsync(
        Guid coachId,
        Guid? packageId,
        Guid currencyId,
        Guid? paymentAccountId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (packageId.HasValue &&
            (await packages.FindAsync(packageId.Value, cancellationToken))?.CoachId != coachId)
            errors["packageId"] = ["The selected package does not exist for this coach."];
        if (await currencies.FindAsync(currencyId, cancellationToken) is null)
            errors["currencyId"] = ["The selected currency does not exist."];
        if (paymentAccountId.HasValue &&
            (await paymentAccounts.FindAsync(paymentAccountId.Value, cancellationToken))?.CoachId != coachId)
            errors["paymentAccountId"] = ["The selected payment account does not exist for this coach."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static void Validate(SubscriptionInput input)
    {
        var errors = ValidateCommercial(input.DurationMonths, input.Price, input.CurrencyId);
        if (input.PackageId == Guid.Empty) errors["packageId"] = ["A package is required."];
        if (input.RenewalCount is < 0 or > 1000)
            errors["renewalCount"] = ["Renewal count must be between 0 and 1000."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static void Validate(SubscriptionRenewalInput input)
    {
        var errors = ValidateCommercial(input.DurationMonths, input.Price, input.CurrencyId);
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static Dictionary<string, string[]> ValidateCommercial(
        int durationMonths,
        decimal price,
        Guid currencyId)
    {
        var errors = new Dictionary<string, string[]>();
        if (currencyId == Guid.Empty) errors["currencyId"] = ["A currency is required."];
        if (durationMonths is < 1 or > 120)
            errors["durationMonths"] = ["Duration must be between 1 and 120 months."];
        if (price is < 0.01m or > 1_000_000m)
            errors["price"] = ["Price must be between 0.01 and 1000000."];
        return errors;
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
