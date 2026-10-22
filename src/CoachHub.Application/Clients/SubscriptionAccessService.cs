using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Localization;
using CoachHub.Application.Settings;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Clients;

public sealed record FeatureAccessResponse(ClientFeature Feature, FeatureAccess Access);

/// <summary>What the signed-in client may do now (§57): their subscription status and each feature's access.</summary>
public sealed record MyAccessResponse(
    SubscriptionStatus SubscriptionStatus,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    IReadOnlyList<FeatureAccessResponse> Features);

/// <summary>Whether a request to a client feature may go ahead, and why not.</summary>
/// <summary>Whether the client may use a feature; a refusal carries its message (in the request's language) and catalog code.</summary>
public sealed record AccessDecision(bool Allowed, string? Message, string? Code = null);

/// <summary>
/// Applies the Administrator's access rules (§47, §57) to the client area. Reading needs Full or Read-only access;
/// writing needs Full. Rules never delete data; restoring access shows everything again.
/// </summary>
public sealed class SubscriptionAccessService(IClientRepository clients, SettingsService settings, TimeProvider timeProvider)
{
    public async Task<MyAccessResponse> MineAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await clients.FindAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
        var today = Today();
        var status = client.GetSubscriptionStatus(today);
        var platform = await settings.PlatformAsync(cancellationToken);
        var current = client.Subscriptions.Where(x => x.StatusOn(today) == status).OrderByDescending(x => x.EndDate).FirstOrDefault();
        return new MyAccessResponse(status, current?.StartDate, current?.EndDate,
            Enum.GetValues<ClientFeature>().Select(feature => new FeatureAccessResponse(feature, platform.AccessFor(status, feature))).ToArray());
    }

    public async Task<AccessDecision> CheckAsync(Guid clientId, ClientFeature feature, bool write, CancellationToken cancellationToken)
    {
        var client = await clients.FindAsync(clientId, cancellationToken);
        // An unknown client is not this rule's business: the endpoint answers 404 itself.
        if (client is null) return new AccessDecision(true, null);
        var status = client.GetSubscriptionStatus(Today());
        var access = (await settings.PlatformAsync(cancellationToken)).AccessFor(status, feature);
        return access switch
        {
            FeatureAccess.Full => new AccessDecision(true, null),
            FeatureAccess.ReadOnly when !write => new AccessDecision(true, null),
            FeatureAccess.ReadOnly => Refusal("access.readOnly", status, feature),
            _ => Refusal("access.unavailable", status, feature)
        };
    }

    private static AccessDecision Refusal(string code, SubscriptionStatus status, ClientFeature feature)
    {
        var language = UiCulture.Current;
        var reason = status is SubscriptionStatus.Expired or SubscriptionStatus.Suspended or SubscriptionStatus.Pending
            ? status.ToString()
            : nameof(SubscriptionStatus.Inactive);
        return new AccessDecision(false, Messages.Text(code, language,
            Messages.Text($"access.status.{reason}", language), Messages.Text($"access.feature.{feature}", language)), code);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
