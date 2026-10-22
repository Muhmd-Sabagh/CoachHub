using System.Text.Json.Serialization;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Settings;

/// <summary>Measurement units shown to users (§49). Values are always stored metric.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitSystem>))]
public enum UnitSystem
{
    Metric,
    Imperial
}

/// <summary>
/// Platform-wide settings (§49), one row. Secrets such as SMTP credentials stay in configuration;
/// this holds what an Administrator decides in the product.
/// </summary>
public sealed class PlatformSettings : Entity
{
    /// <summary>The single row's identifier.</summary>
    public static readonly Guid SingletonId = new("7f0c2a52-1d5e-4b0a-9c39-5a7b1e2f0001");

    public const int MaximumDurationPresets = 8;

    private readonly List<SubscriptionAccessRule> _accessRules = [];

    private PlatformSettings() { }

    /// <summary>"en" or "ar".</summary>
    public string DefaultLanguage { get; private set; } = "en";
    public UnitSystem Units { get; private set; } = UnitSystem.Metric;

    /// <summary>Whether coach workspace messages may be emailed. Account security emails always are.</summary>
    public bool EmailNotificationsEnabled { get; private set; } = true;

    /// <summary>Whether coach workspace messages may be sent by WhatsApp.</summary>
    public bool WhatsAppNotificationsEnabled { get; private set; } = true;

    /// <summary>The display name on platform emails; null uses the platform brand name.</summary>
    public string? EmailSenderName { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The subscription lengths offered as quick choices (§47), as "1,3,6,12".</summary>
    public string DurationPresetList { get; private set; } = string.Join(',', SubscriptionAccessDefaults.DurationPresets);

    /// <summary>The Administrator's access rules (§57); cells without a rule use <see cref="SubscriptionAccessDefaults"/>.</summary>
    public IReadOnlyCollection<SubscriptionAccessRule> AccessRules => _accessRules;

    public IReadOnlyList<int> DurationPresets => DurationPresetList.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

    /// <summary>A feature's access for a subscription status: full when active, otherwise the rule or its default.</summary>
    public FeatureAccess AccessFor(SubscriptionStatus status, ClientFeature feature) =>
        status == SubscriptionStatus.Active
            ? FeatureAccess.Full
            : _accessRules.FirstOrDefault(x => x.Status == status && x.Feature == feature)?.Access ?? SubscriptionAccessDefaults.For(status);

    /// <summary>Sets the duration presets and the access rules; cells left out return to their default.</summary>
    public void SetSubscriptionRules(
        IReadOnlyCollection<int> durationPresets,
        IReadOnlyCollection<(SubscriptionStatus Status, ClientFeature Feature, FeatureAccess Access)> rules,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(durationPresets);
        ArgumentNullException.ThrowIfNull(rules);
        if (durationPresets.Count is 0 or > MaximumDurationPresets)
            throw new ArgumentException($"Offer between 1 and {MaximumDurationPresets} durations.", nameof(durationPresets));
        if (durationPresets.Any(x => x is < 1 or > 120) || durationPresets.Distinct().Count() != durationPresets.Count)
            throw new ArgumentException("Durations are distinct numbers of months from 1 to 120.", nameof(durationPresets));
        foreach (var rule in rules)
        {
            if (!SubscriptionAccessDefaults.RestrictableStatuses.Contains(rule.Status))
                throw new ArgumentException("Rules apply to expired, suspended, pending or missing subscriptions.", nameof(rules));
            if (!Enum.IsDefined(rule.Feature) || !Enum.IsDefined(rule.Access)) throw new ArgumentException("Unknown feature or access.", nameof(rules));
        }
        if (rules.Select(x => (x.Status, x.Feature)).Distinct().Count() != rules.Count)
            throw new ArgumentException("Each status and feature has one rule.", nameof(rules));

        DurationPresetList = string.Join(',', durationPresets.Order());
        _accessRules.RemoveAll(existing => !rules.Any(x => x.Status == existing.Status && x.Feature == existing.Feature));
        foreach (var rule in rules)
        {
            var existing = _accessRules.FirstOrDefault(x => x.Status == rule.Status && x.Feature == rule.Feature);
            if (existing is null || existing.Access != rule.Access)
            {
                if (existing is not null) _accessRules.Remove(existing);
                _accessRules.Add(SubscriptionAccessRule.Create(Id, rule.Status, rule.Feature, rule.Access));
            }
        }
        UpdatedAt = now;
    }

    public static PlatformSettings CreateDefault(DateTimeOffset now) => new() { Id = SingletonId, UpdatedAt = now };

    public void Update(
        string defaultLanguage,
        UnitSystem units,
        bool emailNotificationsEnabled,
        bool whatsAppNotificationsEnabled,
        string? emailSenderName,
        DateTimeOffset now)
    {
        var language = defaultLanguage?.Trim().ToLowerInvariant();
        if (language is not ("en" or "ar")) throw new ArgumentException("Choose English (en) or Arabic (ar).", nameof(defaultLanguage));
        if (!Enum.IsDefined(units)) throw new ArgumentException("Choose metric or imperial units.", nameof(units));
        var sender = string.IsNullOrWhiteSpace(emailSenderName) ? null : emailSenderName.Trim();
        if (sender is { Length: > 100 } || sender?.IndexOfAny(['<', '>', '"', '\r', '\n']) >= 0)
            throw new ArgumentException("Use a plain name of at most 100 characters.", nameof(emailSenderName));

        DefaultLanguage = language;
        Units = units;
        EmailNotificationsEnabled = emailNotificationsEnabled;
        WhatsAppNotificationsEnabled = whatsAppNotificationsEnabled;
        EmailSenderName = sender;
        UpdatedAt = now;
    }
}
