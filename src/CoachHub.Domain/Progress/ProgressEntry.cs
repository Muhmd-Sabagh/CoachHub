using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Progress;

/// <summary>
/// Where a tracking entry came from (§31, §32, §54). Clients enter <see cref="Manual"/> values; the other sources are
/// for future health integrations (<c>IHealthDataProvider</c>), which identify their samples by an external ID.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EntrySource>))]
public enum EntrySource
{
    Manual,
    AppleHealth,
    HealthConnect,
    Wearable
}

/// <summary>
/// A typed, dated value the client tracked (D3): weight, body fat, a measurement, sleep, steps or water.
/// Tenant-owned and a client row, so the client and their coach see it and nobody else.
/// </summary>
public abstract class ProgressEntry : Entity, ITenantOwned
{
    public const int MaximumNotesLength = 500;
    public const int MaximumExternalIdLength = 200;

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public EntrySource Source { get; private set; }

    /// <summary>The provider's identifier for an imported sample; null for manual entries.</summary>
    public string? ExternalId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    protected void Initialize(
        Guid coachId, Guid clientId, DateOnly date, EntrySource source, string? externalId, string? notes, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (!Enum.IsDefined(source)) throw new ArgumentException("Unknown entry source.", nameof(source));
        var external = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        if (external is { Length: > MaximumExternalIdLength })
            throw new ArgumentException($"An external ID has at most {MaximumExternalIdLength} characters.", nameof(externalId));
        if (source == EntrySource.Manual && external is not null)
            throw new ArgumentException("Manual entries have no external ID.", nameof(externalId));
        if (source != EntrySource.Manual && external is null)
            throw new ArgumentException("Imported entries carry the provider's ID.", nameof(externalId));
        CoachId = TenantKey.Required(coachId);
        ClientId = clientId;
        Date = date;
        Source = source;
        ExternalId = external;
        CreatedAt = now;
        SetNotes(notes, now);
    }

    protected void SetNotes(string? notes, DateTimeOffset now)
    {
        var value = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (value is { Length: > MaximumNotesLength })
            throw new ArgumentException($"Notes have at most {MaximumNotesLength} characters.", nameof(notes));
        Notes = value;
        UpdatedAt = now;
    }

    protected static decimal Measure(decimal value, decimal minimum, decimal maximum, int decimals, string name, string unit)
    {
        if (value < minimum || value > maximum || decimal.Round(value, decimals) != value)
            throw new ArgumentException($"Enter {minimum}–{maximum} {unit} with at most {decimals} decimal{(decimals == 1 ? "" : "s")}.", name);
        return value;
    }

    protected static int Count(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum) throw new ArgumentException($"Must be between {minimum} and {maximum}.", name);
        return value;
    }
}
