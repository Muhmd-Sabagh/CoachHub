using System.Text.Json.Serialization;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Progress;

/// <summary>What a health integration can report (§31, §32, §54).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HealthMetric>))]
public enum HealthMetric
{
    WeightKg,
    BodyFatPercent,
    SleepHours,
    Steps,
    WaterMl
}

/// <summary>One sample from a provider; <paramref name="ExternalId"/> identifies it there, so imports are idempotent.</summary>
public sealed record HealthSample(HealthMetric Metric, DateOnly Date, decimal Value, string ExternalId);

/// <summary>
/// A future health data source — Apple Health, Health Connect or a wearable (§31, §32, §54). There is no
/// implementation yet: tracking entries already carry <see cref="EntrySource"/> and an external ID, so an importer can
/// store samples next to manual entries without a schema change.
/// </summary>
public interface IHealthDataProvider
{
    EntrySource Source { get; }

    Task<IReadOnlyList<HealthSample>> ReadAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
