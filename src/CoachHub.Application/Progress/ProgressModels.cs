using CoachHub.Application.FoodTracking;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Progress;

public sealed record WeightInput(decimal WeightKg, string? Notes = null);
public sealed record BodyFatInput(decimal Percent, string? Notes = null);

/// <summary>A day's measurements: each site with a value is set; a site with <c>null</c> is removed.</summary>
public sealed record MeasurementsInput(IReadOnlyList<MeasurementValueInput> Values, string? Notes = null);
public sealed record MeasurementValueInput(MeasurementSite Site, decimal? ValueCm);

/// <summary>A night's sleep: the hours, or bedtime and wake time (hours then follow); quality 1–5.</summary>
public sealed record SleepInput(decimal? Hours = null, int? Quality = null, TimeOnly? Bedtime = null, TimeOnly? WakeTime = null, string? Notes = null);
public sealed record StepsInput(int Steps, string? Notes = null);

/// <summary>One drink of water on <paramref name="Date"/>.</summary>
public sealed record WaterInput(DateOnly Date, int AmountMl, string? Notes = null);

/// <summary>A photo's details; the image itself is uploaded with them.</summary>
public sealed record ProgressPhotoInput(DateOnly Date, PhotoPose Pose, string? Label = null, string? Notes = null);

/// <summary>An entry of a series, as stored.</summary>
public sealed record SeriesEntry(Guid Id, DateOnly Date, decimal Value, EntrySource Source, string? Notes);

/// <summary>A measure's summary over the client's history and its entries within the range (for the graph).</summary>
public sealed record SeriesResponse(SeriesSummary Summary, IReadOnlyList<SeriesEntry> Entries);
public sealed record MeasurementSeriesResponse(MeasurementSite Site, SeriesSummary Summary, IReadOnlyList<SeriesEntry> Entries);

/// <summary>
/// Body tracking (§24, §25, §27): weight, body fat and each measurement site. The profile's starting values (P5) are
/// shown for context; the series start is the first entry.
/// </summary>
public sealed record BodyProgressResponse(
    DateOnly From, DateOnly To, SeriesResponse Weight, SeriesResponse BodyFat,
    IReadOnlyList<MeasurementSeriesResponse> Measurements,
    decimal? ProfileStartingWeightKg, decimal? ProfileStartingBodyFatPercent);

public sealed record SleepEntryResponse(
    Guid Id, DateOnly Date, decimal Hours, int? Quality, TimeOnly? Bedtime, TimeOnly? WakeTime, EntrySource Source, string? Notes);
public sealed record StepsEntryResponse(Guid Id, DateOnly Date, int Steps, EntrySource Source, string? Notes);
public sealed record WaterEntryResponse(Guid Id, DateOnly Date, int AmountMl, EntrySource Source, string? Notes, DateTimeOffset CreatedAt);

/// <summary>A day of lifestyle tracking with its completion of each target (0–100).</summary>
public sealed record LifestyleDayResponse(
    DateOnly Date, SleepEntryResponse? Sleep, StepsEntryResponse? Steps, int WaterMl, IReadOnlyList<WaterEntryResponse> Water,
    decimal? SleepCompletionPercent, decimal? StepsCompletionPercent, decimal? WaterCompletionPercent);

/// <summary>Sleep, steps and water (§31–§33) for each day of the range up to today, the totals, and each ISO week.</summary>
public sealed record LifestyleResponse(
    DateOnly From, DateOnly To, LifestyleTargetValues Targets, IReadOnlyList<LifestyleDayResponse> Days,
    LifestyleSummary Summary, IReadOnlyList<LifestyleSummary> Weeks);

/// <summary>The client's own targets, the coach's defaults, and what applies (each value: the client's, else the default).</summary>
public sealed record LifestyleTargetsResponse(LifestyleTargetValues Client, LifestyleTargetValues CoachDefaults, LifestyleTargetValues Effective);

public sealed record ProgressPhotoResponse(
    Guid Id, DateOnly Date, PhotoPose Pose, string? Label, Guid MediaId, string? Notes, DateTimeOffset CreatedAt);

/// <summary>A measurement site's current value and change since its first entry.</summary>
public sealed record MeasurementChange(MeasurementSite Site, SeriesSummary Summary);

public sealed record BodyDashboard(
    SeriesSummary Weight, SeriesSummary BodyFat, IReadOnlyList<MeasurementChange> Measurements,
    int PhotoCount, ProgressPhotoResponse? FirstPhoto, ProgressPhotoResponse? LatestPhoto);

/// <summary>Training for the dashboard: the period's figures, the latest PRs, and the current e1RM bests (strength).</summary>
public sealed record TrainingDashboard(
    TrainingPeriodSummary Summary, IReadOnlyList<PersonalRecordResponse> RecentRecords, IReadOnlyList<PersonalRecordResponse> Strength);

/// <summary>
/// The client progress dashboard (§37): body, nutrition (P8), training (P10) and lifestyle for a range, all from the
/// same services the detailed screens use. The check-in section (latest check-in, adherence, coach feedback) is added
/// by the check-in workflow (P12).
/// </summary>
public sealed record ProgressDashboardResponse(
    DateOnly From, DateOnly To, BodyDashboard Body, NutritionPeriodSummary Nutrition, TrainingDashboard Training,
    LifestyleSummary Lifestyle, LifestyleTargetValues Targets);
