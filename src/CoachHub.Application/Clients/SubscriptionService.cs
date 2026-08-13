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
        if (await repository.FindAsync(clientId, cancellationToken) is null)
            throw new NotFoundException("Client", clientId);
        Validate(input);
        await ValidateReferencesAsync(
            input.PackageId,
            input.CurrencyId,
            input.PaymentAccountId,
            cancellationToken);
        var subscription = Subscription.Create(
            clientId,
            input.PackageId,
            input.StartDate,
            input.DurationMonths,
            input.Price,
            input.CurrencyId,
            input.PaymentAccountId,
            input.RenewalCount);
        await repository.AddSubscriptionAsync(subscription, cancellationToken);
        return Map(subscription, Today());
    }

    public async Task<SubscriptionResponse> UpdateAsync(
        Guid clientId,
        Guid id,
        SubscriptionInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(
            input.PackageId,
            input.CurrencyId,
            input.PaymentAccountId,
            cancellationToken);
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
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
        return Map(subscription, Today());
    }

    public async Task<SubscriptionResponse> RenewAsync(
        Guid clientId,
        Guid id,
        SubscriptionRenewalInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(null, input.CurrencyId, input.PaymentAccountId, cancellationToken);
        var subscription = await FindRequiredAsync(clientId, id, cancellationToken);
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
        return Map(subscription, Today());
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

    internal static SubscriptionResponse Map(Subscription subscription, DateOnly today) => new(
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
            .ToArray());

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

    private async Task ValidateReferencesAsync(
        Guid? packageId,
        Guid currencyId,
        Guid? paymentAccountId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (packageId.HasValue && await packages.FindAsync(packageId.Value, cancellationToken) is null)
            errors["packageId"] = ["The selected package does not exist."];
        if (await currencies.FindAsync(currencyId, cancellationToken) is null)
            errors["currencyId"] = ["The selected currency does not exist."];
        if (paymentAccountId.HasValue &&
            await paymentAccounts.FindAsync(paymentAccountId.Value, cancellationToken) is null)
            errors["paymentAccountId"] = ["The selected payment account does not exist."];
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
