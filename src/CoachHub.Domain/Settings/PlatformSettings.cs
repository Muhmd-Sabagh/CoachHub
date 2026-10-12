using System.Text.Json.Serialization;
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
