using CoachHub.Domain.Progress;
using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Tests.Progress;

/// <summary>Typed body and lifestyle entries (§24–§27, §31–§33, D3) and their targets.</summary>
public sealed class ProgressEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();

    [Fact]
    public void Body_entries_hold_typed_values_in_range()
    {
        var weight = WeightEntry.Create(Coach, Client, Today, 82.45m, " after breakfast ", Now);
        Assert.Equal((82.45m, "after breakfast", EntrySource.Manual), (weight.WeightKg, weight.Notes, weight.Source));
        weight.Change(81.9m, null, Now.AddHours(1));
        Assert.Equal((81.9m, (string?)null, Now.AddHours(1)), (weight.WeightKg, weight.Notes, weight.UpdatedAt));
        Assert.Throws<ArgumentException>(() => WeightEntry.Create(Coach, Client, Today, 19.9m, null, Now));
        Assert.Throws<ArgumentException>(() => WeightEntry.Create(Coach, Client, Today, 80.123m, null, Now));

        Assert.Equal(18.5m, BodyFatEntry.Create(Coach, Client, Today, 18.5m, null, Now).Percent);
        Assert.Throws<ArgumentException>(() => BodyFatEntry.Create(Coach, Client, Today, 18.55m, null, Now));
        Assert.Throws<ArgumentException>(() => BodyFatEntry.Create(Coach, Client, Today, 80m, null, Now));

        var waist = BodyMeasurement.Create(Coach, Client, Today, MeasurementSite.Waist, 84.5m, null, Now);
        Assert.Equal((MeasurementSite.Waist, 84.5m), (waist.Site, waist.ValueCm));
        Assert.Throws<ArgumentException>(() => BodyMeasurement.Create(Coach, Client, Today, (MeasurementSite)20, 80m, null, Now));
        Assert.Throws<ArgumentException>(() => BodyMeasurement.Create(Coach, Client, Today, MeasurementSite.Neck, 4m, null, Now));
    }

    [Fact]
    public void Entries_keep_their_source_and_owner()
    {
        var imported = StepsEntry.Create(Coach, Client, Today, 9500, null, Now, EntrySource.HealthConnect, "hc-123");
        Assert.Equal((EntrySource.HealthConnect, "hc-123"), (imported.Source, imported.ExternalId));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Coach, Client, Today, 1, null, Now, EntrySource.Manual, "x"));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Coach, Client, Today, 1, null, Now, EntrySource.AppleHealth));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Guid.Empty, Client, Today, 1, null, Now));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Coach, Guid.Empty, Today, 1, null, Now));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Coach, Client, Today, -1, null, Now));
        Assert.Throws<ArgumentException>(() => StepsEntry.Create(Coach, Client, Today, 1, new string('n', 501), Now));
    }

    [Fact]
    public void Sleep_hours_follow_bedtime_and_wake_time_across_midnight()
    {
        var night = SleepEntry.Create(Coach, Client, Today, null, 4, new TimeOnly(23, 15), new TimeOnly(6, 45), null, Now);
        Assert.Equal((7.5m, 4), (night.Hours, night.Quality!.Value));
        Assert.Equal(1.5m, SleepEntry.HoursBetween(new TimeOnly(13, 0), new TimeOnly(14, 30)));
        // Hours entered win over the times.
        Assert.Equal(7m, SleepEntry.Create(Coach, Client, Today, 7m, null, new TimeOnly(23, 0), new TimeOnly(7, 0), null, Now).Hours);
        Assert.Throws<ArgumentException>(() => SleepEntry.Create(Coach, Client, Today, null, null, null, null, null, Now));
        Assert.Throws<ArgumentException>(() => SleepEntry.Create(Coach, Client, Today, 7m, null, new TimeOnly(23, 0), null, null, Now));
        Assert.Throws<ArgumentException>(() => SleepEntry.Create(Coach, Client, Today, 7m, 6, null, null, null, Now));
        Assert.Throws<ArgumentException>(() => SleepEntry.Create(Coach, Client, Today, 25m, null, null, null, null, Now));
    }

    [Fact]
    public void Water_is_logged_per_drink()
    {
        Assert.Equal(500, WaterEntry.Create(Coach, Client, Today, 500, null, Now).AmountMl);
        Assert.Throws<ArgumentException>(() => WaterEntry.Create(Coach, Client, Today, 0, null, Now));
        Assert.Throws<ArgumentException>(() => WaterEntry.Create(Coach, Client, Today, 10_001, null, Now));
    }

    [Fact]
    public void Photos_have_a_pose_and_their_image()
    {
        var media = Guid.NewGuid();
        var photo = ProgressPhoto.Create(Coach, Client, Today, PhotoPose.Custom, " flexed ", media, null, Now);
        Assert.Equal((PhotoPose.Custom, "flexed", media), (photo.Pose, photo.Label, photo.MediaId));
        photo.Change(Today.AddDays(-1), PhotoPose.Front, null, "morning");
        Assert.Equal((Today.AddDays(-1), PhotoPose.Front, (string?)null), (photo.Date, photo.Pose, photo.Label));
        Assert.Throws<ArgumentException>(() => ProgressPhoto.Create(Coach, Client, Today, PhotoPose.Back, null, Guid.Empty, null, Now));
        Assert.Throws<ArgumentException>(() => ProgressPhoto.Create(Coach, Client, Today, (PhotoPose)9, null, media, null, Now));
        Assert.Throws<ArgumentException>(() => photo.Change(Today, PhotoPose.Custom, new string('x', 51), null));
    }

    [Fact]
    public void Targets_are_bounded_and_fall_back_to_the_coach_defaults()
    {
        var targets = LifestyleTargets.Create(Coach, Client, new LifestyleTargetValues(10_000, null, 8m), Now);
        var defaults = new LifestyleTargetValues(8_000, 2_500, 7m);
        Assert.Equal(new LifestyleTargetValues(10_000, 2_500, 8m), targets.Values.Or(defaults));
        Assert.Throws<ArgumentException>(() => targets.Change(new LifestyleTargetValues(50, null, null), Now));
        Assert.Throws<ArgumentException>(() => targets.Change(new LifestyleTargetValues(null, 100, null), Now));
        Assert.Throws<ArgumentException>(() => targets.Change(new LifestyleTargetValues(null, null, 20m), Now));

        var settings = CoachSettings.CreateDefault(Coach, Now);
        Assert.Equal(LifestyleTargetValues.None, settings.DefaultTargets);
        settings.UpdateDefaultTargets(defaults, Now);
        Assert.Equal(defaults, settings.DefaultTargets);
    }
}
