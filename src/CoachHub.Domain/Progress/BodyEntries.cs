using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Progress;

/// <summary>A weigh-in (§24): one per day, in kg.</summary>
public sealed class WeightEntry : ProgressEntry
{
    public const decimal MinimumKg = 20m;
    public const decimal MaximumKg = 400m;

    private WeightEntry() { }

    public decimal WeightKg { get; private set; }

    public static WeightEntry Create(
        Guid coachId, Guid clientId, DateOnly date, decimal weightKg, string? notes, DateTimeOffset now,
        EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        var entry = new WeightEntry();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.WeightKg = Validate(weightKg);
        return entry;
    }

    public void Change(decimal weightKg, string? notes, DateTimeOffset now)
    {
        WeightKg = Validate(weightKg);
        SetNotes(notes, now);
    }

    private static decimal Validate(decimal kg) => Measure(kg, MinimumKg, MaximumKg, 2, "weightKg", "kg");
}

/// <summary>
/// A body fat percentage entered by the client (§27): one per day. It is an estimate for coaching, not a medical
/// diagnosis; the screens say so.
/// </summary>
public sealed class BodyFatEntry : ProgressEntry
{
    private BodyFatEntry() { }

    public decimal Percent { get; private set; }

    public static BodyFatEntry Create(
        Guid coachId, Guid clientId, DateOnly date, decimal percent, string? notes, DateTimeOffset now,
        EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        var entry = new BodyFatEntry();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.Percent = Validate(percent);
        return entry;
    }

    public void Change(decimal percent, string? notes, DateTimeOffset now)
    {
        Percent = Validate(percent);
        SetNotes(notes, now);
    }

    private static decimal Validate(decimal percent) => Measure(percent, 2m, 75m, 1, "percent", "%");
}

/// <summary>Where a body measurement is taken (§25).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeasurementSite>))]
public enum MeasurementSite
{
    Waist,
    Chest,
    Arm,
    Thigh,
    Hip,
    Neck,
    Calf
}

/// <summary>A body measurement in cm (§25): one per site and day.</summary>
public sealed class BodyMeasurement : ProgressEntry
{
    private BodyMeasurement() { }

    public MeasurementSite Site { get; private set; }
    public decimal ValueCm { get; private set; }

    public static BodyMeasurement Create(
        Guid coachId, Guid clientId, DateOnly date, MeasurementSite site, decimal valueCm, string? notes, DateTimeOffset now,
        EntrySource source = EntrySource.Manual, string? externalId = null)
    {
        if (!Enum.IsDefined(site)) throw new ArgumentException("Unknown measurement site.", nameof(site));
        var entry = new BodyMeasurement();
        entry.Initialize(coachId, clientId, date, source, externalId, notes, now);
        entry.Site = site;
        entry.ValueCm = Validate(valueCm);
        return entry;
    }

    public void Change(decimal valueCm, string? notes, DateTimeOffset now)
    {
        ValueCm = Validate(valueCm);
        SetNotes(notes, now);
    }

    private static decimal Validate(decimal cm) => Measure(cm, 5m, 300m, 1, "valueCm", "cm");
}

/// <summary>The pose of a progress photo (§26).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PhotoPose>))]
public enum PhotoPose
{
    Front,
    Side,
    Back,
    Custom
}

/// <summary>
/// A dated progress photo (§26). The image is the client's own media (P2), so only the client and their coach can
/// open it, through the authorized media endpoint.
/// </summary>
public sealed class ProgressPhoto : Entity, ITenantOwned
{
    public const int MaximumLabelLength = 50;

    private ProgressPhoto() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public PhotoPose Pose { get; private set; }

    /// <summary>A name for a custom pose ("flexed", "side left").</summary>
    public string? Label { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProgressPhoto Create(
        Guid coachId, Guid clientId, DateOnly date, PhotoPose pose, string? label, Guid mediaId, string? notes, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (mediaId == Guid.Empty) throw new ArgumentException("The photo is required.", nameof(mediaId));
        var photo = new ProgressPhoto
        {
            CoachId = TenantKey.Required(coachId), ClientId = clientId, MediaId = mediaId, CreatedAt = now
        };
        photo.Change(date, pose, label, notes);
        return photo;
    }

    public void Change(DateOnly date, PhotoPose pose, string? label, string? notes)
    {
        if (!Enum.IsDefined(pose)) throw new ArgumentException("Unknown pose.", nameof(pose));
        var text = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        if (text is { Length: > MaximumLabelLength })
            throw new ArgumentException($"A label has at most {MaximumLabelLength} characters.", nameof(label));
        var note = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (note is { Length: > ProgressEntry.MaximumNotesLength })
            throw new ArgumentException($"Notes have at most {ProgressEntry.MaximumNotesLength} characters.", nameof(notes));
        Date = date;
        Pose = pose;
        Label = text;
        Notes = note;
    }
}
