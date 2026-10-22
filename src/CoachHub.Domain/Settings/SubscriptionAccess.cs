using System.Text.Json.Serialization;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Settings;

/// <summary>A part of the client area the Administrator can restrict by subscription status (§57).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientFeature>))]
public enum ClientFeature
{
    /// <summary>The diet plan and the food diary.</summary>
    Nutrition,
    /// <summary>The workout plan and workout logging.</summary>
    Training,
    /// <summary>Body and lifestyle tracking, photos and fitness tests.</summary>
    Progress,
    FormsAndCheckIns,
    Chat,
    Files,
    Calendar
}

/// <summary>Full use, read-only (history stays visible, nothing can be written), or blocked.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeatureAccess>))]
public enum FeatureAccess
{
    Full,
    ReadOnly,
    Blocked
}

/// <summary>One cell of the Administrator's access rules: a feature's access for a subscription status.</summary>
public sealed class SubscriptionAccessRule : Entity
{
    private SubscriptionAccessRule() { }

    public Guid PlatformSettingsId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public ClientFeature Feature { get; private set; }
    public FeatureAccess Access { get; private set; }

    internal static SubscriptionAccessRule Create(Guid settingsId, SubscriptionStatus status, ClientFeature feature, FeatureAccess access) =>
        new() { PlatformSettingsId = settingsId, Status = status, Feature = feature, Access = access };
}

/// <summary>
/// Access rules (§47, §57). Active subscriptions always have full access; the other statuses follow the rules, with
/// these defaults (owner decision 2026-10-22): Expired and Suspended read-only, Pending and no subscription full.
/// Rules only restrict access; client data is never deleted.
/// </summary>
public static class SubscriptionAccessDefaults
{
    public static readonly IReadOnlyList<SubscriptionStatus> RestrictableStatuses =
        [SubscriptionStatus.Expired, SubscriptionStatus.Suspended, SubscriptionStatus.Pending, SubscriptionStatus.Inactive];

    public static readonly IReadOnlyList<int> DurationPresets = [1, 3, 6, 12];

    public static FeatureAccess For(SubscriptionStatus status) =>
        status is SubscriptionStatus.Expired or SubscriptionStatus.Suspended ? FeatureAccess.ReadOnly : FeatureAccess.Full;
}
