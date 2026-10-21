using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Media;
using CoachHub.Application.Settings;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Progress;

namespace CoachHub.Application.Progress;

/// <summary>
/// Body (§24–§27) and lifestyle (§31–§33) tracking, the client's targets, and the progress dashboard (§37).
/// Clients write their own entries and photos; coaches read them and set the targets. Tenant filters decide who
/// sees which client.
/// </summary>
public sealed class ProgressService(
    IProgressRepository repository,
    IClientRepository clients,
    SettingsService settings,
    MediaService media,
    TimeProvider timeProvider)
{
    /// <summary>The longest range, and how far back entries can be written.</summary>
    public const int MaximumRangeDays = 366;

    public async Task<BodyProgressResponse> BodyAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        await ClientCoachAsync(clientId, cancellationToken);
        var weights = await repository.ListAsync<WeightEntry>(clientId, null, null, cancellationToken);
        var bodyFat = await repository.ListAsync<BodyFatEntry>(clientId, null, null, cancellationToken);
        var measurements = await repository.ListAsync<BodyMeasurement>(clientId, null, null, cancellationToken);
        var profile = await clients.FindProfileAsync(clientId, cancellationToken);
        return new BodyProgressResponse(from, to,
            Series(weights.Select(x => (Entry(x, x.WeightKg), x.Date)), from, to, 2),
            Series(bodyFat.Select(x => (Entry(x, x.Percent), x.Date)), from, to, 1),
            Enum.GetValues<MeasurementSite>().Select(site =>
            {
                var series = Series(measurements.Where(x => x.Site == site).Select(x => (Entry(x, x.ValueCm), x.Date)), from, to, 1);
                return new MeasurementSeriesResponse(site, series.Summary, series.Entries);
            }).ToArray(),
            profile?.StartingWeightKg, profile?.StartingBodyFatPercent);
    }

    /// <summary>Records the day's weight (§24), replacing an earlier value of the same day.</summary>
    public Task SetWeightAsync(Guid clientId, DateOnly date, WeightInput input, CancellationToken cancellationToken) =>
        UpsertDailyAsync<WeightEntry>(clientId, date,
            (coachId, now) => WeightEntry.Create(coachId, clientId, date, input.WeightKg, input.Notes, now),
            (entry, now) => entry.Change(input.WeightKg, input.Notes, now), "weightKg", cancellationToken);

    public Task DeleteWeightAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        DeleteDailyAsync<WeightEntry>(clientId, date, cancellationToken);

    /// <summary>Records the day's body fat % (§27); an estimate, not a medical diagnosis.</summary>
    public Task SetBodyFatAsync(Guid clientId, DateOnly date, BodyFatInput input, CancellationToken cancellationToken) =>
        UpsertDailyAsync<BodyFatEntry>(clientId, date,
            (coachId, now) => BodyFatEntry.Create(coachId, clientId, date, input.Percent, input.Notes, now),
            (entry, now) => entry.Change(input.Percent, input.Notes, now), "percent", cancellationToken);

    public Task DeleteBodyFatAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        DeleteDailyAsync<BodyFatEntry>(clientId, date, cancellationToken);

    /// <summary>Sets the day's measurements (§25): sites with a value are set, sites with null removed, others kept.</summary>
    public async Task SetMeasurementsAsync(Guid clientId, DateOnly date, MeasurementsInput input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        if (input.Values is not { Count: > 0 }) throw Validation("values", "Enter at least one measurement.");
        if (input.Values.Select(x => x.Site).Distinct().Count() != input.Values.Count) throw Validation("values", "Each site appears once.");
        var existing = (await repository.FindOnAsync<BodyMeasurement>(clientId, date, cancellationToken)).ToDictionary(x => x.Site);
        var now = timeProvider.GetUtcNow();
        DomainValidation.Run(() =>
        {
            foreach (var value in input.Values)
            {
                if (!Enum.IsDefined(value.Site)) throw new ArgumentException("Unknown measurement site.", "site");
                var current = existing.GetValueOrDefault(value.Site);
                if (value.ValueCm is not { } cm)
                {
                    if (current is not null) repository.Remove(current);
                }
                else if (current is null) repository.Add(BodyMeasurement.Create(coachId, clientId, date, value.Site, cm, input.Notes, now));
                else current.Change(cm, input.Notes, now);
            }
        }, "values");
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<LifestyleResponse> LifestyleAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        EnsureRange(from, to);
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var targets = (await TargetsForAsync(clientId, coachId, cancellationToken)).Effective;
        var last = to < Today() ? to : Today();
        var sleep = (await repository.ListAsync<SleepEntry>(clientId, from, to, cancellationToken)).ToDictionary(x => x.Date);
        var steps = (await repository.ListAsync<StepsEntry>(clientId, from, to, cancellationToken)).ToDictionary(x => x.Date);
        var water = (await repository.ListAsync<WaterEntry>(clientId, from, to, cancellationToken)).ToLookup(x => x.Date);
        var days = new List<LifestyleDayResponse>();
        var figures = new List<LifestyleDayFigures>();
        for (var date = from; date <= last; date = date.AddDays(1))
        {
            var night = sleep.GetValueOrDefault(date);
            var walked = steps.GetValueOrDefault(date);
            var drinks = water[date].OrderBy(x => x.CreatedAt).ToArray();
            int? ml = drinks.Length == 0 ? null : drinks.Sum(x => x.AmountMl);
            figures.Add(new LifestyleDayFigures(date, night?.Hours, night?.Quality, walked?.Steps, ml));
            days.Add(new LifestyleDayResponse(date, night is null ? null : Map(night), walked is null ? null : Map(walked),
                ml ?? 0, drinks.Select(Map).ToArray(),
                ProgressStatistics.Completion(night?.Hours, targets.SleepHours),
                ProgressStatistics.Completion(walked?.Steps, targets.Steps),
                ProgressStatistics.Completion(ml, targets.WaterMl)));
        }
        var weeks = new List<LifestyleSummary>();
        for (var week = NutritionAdherence.WeekStart(from); week <= last; week = week.AddDays(7))
            weeks.Add(ProgressStatistics.Summarize(Max(week, from), Min(week.AddDays(6), to), figures, targets));
        return new LifestyleResponse(from, to, targets, days, ProgressStatistics.Summarize(from, to, figures, targets), weeks);
    }

    /// <summary>Records the night's sleep (§31), dated by the morning it ended.</summary>
    public Task SetSleepAsync(Guid clientId, DateOnly date, SleepInput input, CancellationToken cancellationToken) =>
        UpsertDailyAsync<SleepEntry>(clientId, date,
            (coachId, now) => SleepEntry.Create(coachId, clientId, date, input.Hours, input.Quality, input.Bedtime, input.WakeTime, input.Notes, now),
            (entry, now) => entry.Change(input.Hours, input.Quality, input.Bedtime, input.WakeTime, input.Notes, now), "hours", cancellationToken);

    public Task DeleteSleepAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        DeleteDailyAsync<SleepEntry>(clientId, date, cancellationToken);

    public Task SetStepsAsync(Guid clientId, DateOnly date, StepsInput input, CancellationToken cancellationToken) =>
        UpsertDailyAsync<StepsEntry>(clientId, date,
            (coachId, now) => StepsEntry.Create(coachId, clientId, date, input.Steps, input.Notes, now),
            (entry, now) => entry.Change(input.Steps, input.Notes, now), "steps", cancellationToken);

    public Task DeleteStepsAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        DeleteDailyAsync<StepsEntry>(clientId, date, cancellationToken);

    /// <summary>Logs a drink of water (§33); the day's consumption is the sum.</summary>
    public async Task<WaterEntryResponse> AddWaterAsync(Guid clientId, WaterInput input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(input.Date);
        var entry = DomainValidation.Run(() => WaterEntry.Create(coachId, clientId, input.Date, input.AmountMl, input.Notes,
            timeProvider.GetUtcNow()), "amountMl");
        repository.Add(entry);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entry);
    }

    public async Task DeleteWaterAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var entry = await repository.FindAsync<WaterEntry>(id, cancellationToken);
        if (entry is null || entry.ClientId != clientId) throw new NotFoundException("Water entry", id);
        EnsureWritable(entry.Date);
        repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<LifestyleTargetsResponse> TargetsAsync(Guid clientId, CancellationToken cancellationToken) =>
        await TargetsForAsync(clientId, await ClientCoachAsync(clientId, cancellationToken), cancellationToken);

    /// <summary>The coach sets the client's daily targets; a null value falls back to the coach's default.</summary>
    public async Task<LifestyleTargetsResponse> SetTargetsAsync(Guid clientId, LifestyleTargetValues input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var targets = await repository.FindTargetsAsync(clientId, cancellationToken);
        DomainValidation.Run(() =>
        {
            if (targets is null) repository.Add(LifestyleTargets.Create(coachId, clientId, input, now));
            else targets.Change(input, now);
        }, "targets");
        await repository.SaveChangesAsync(cancellationToken);
        return await TargetsForAsync(clientId, coachId, cancellationToken);
    }

    /// <summary>The photo timeline (§26), newest first, optionally of one pose.</summary>
    public async Task<IReadOnlyList<ProgressPhotoResponse>> PhotosAsync(Guid clientId, PhotoPose? pose, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        return (await repository.ListPhotosAsync(clientId, pose, cancellationToken))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt).Select(Map).ToArray();
    }

    /// <summary>
    /// Stores a progress photo (§26). The image becomes the client's own media (P2): visible to the client and their
    /// coach only, through the authorized media endpoint.
    /// </summary>
    public async Task<ProgressPhotoResponse> AddPhotoAsync(
        Guid clientId, Guid uploadedByUserId, ProgressPhotoInput input, Stream content, string fileName, string contentType,
        long sizeBytes, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(input.Date);
        var now = timeProvider.GetUtcNow();
        // Check the details before storing the file.
        DomainValidation.Run(() => ProgressPhoto.Create(coachId, clientId, input.Date, input.Pose, input.Label, Guid.NewGuid(), input.Notes, now), "photo");
        var stored = await media.UploadAsync(content, fileName, contentType, sizeBytes,
            MediaOwner.OfClient(coachId, clientId, uploadedByUserId), cancellationToken);
        var photo = ProgressPhoto.Create(coachId, clientId, input.Date, input.Pose, input.Label, stored.Id, input.Notes, now);
        try
        {
            repository.Add(photo);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await media.DeleteAsync(stored.Id, CancellationToken.None);
            throw;
        }
        return Map(photo);
    }

    public async Task<ProgressPhotoResponse> ChangePhotoAsync(Guid clientId, Guid id, ProgressPhotoInput input, CancellationToken cancellationToken)
    {
        var photo = await RequiredPhotoAsync(clientId, id, cancellationToken);
        EnsureWritable(input.Date);
        DomainValidation.Run(() => photo.Change(input.Date, input.Pose, input.Label, input.Notes), "photo");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(photo);
    }

    /// <summary>
    /// Deletes a photo and its image. An image a check-in answer also holds (P12) stays with the check-in.
    /// </summary>
    public async Task DeletePhotoAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        var photo = await RequiredPhotoAsync(clientId, id, cancellationToken);
        repository.Remove(photo);
        await repository.SaveChangesAsync(cancellationToken);
        try
        {
            await media.DeleteAsync(photo.MediaId, cancellationToken);
        }
        catch (ConflictException)
        {
            // Still referenced by a form answer: the check-in keeps its photo.
        }
    }

    /// <summary>
    /// Checks a measure answered in a form (P12) the way its entry would, so a bad value fails before anything is
    /// stored. Photos are checked by the form (the client's own media).
    /// </summary>
    public static void CheckMetric(QuestionMetric metric, decimal? value, string field)
    {
        if (QuestionMetrics.IsPhoto(metric)) return;
        if (value is not { } v) throw Validation(field, "Enter a number.");
        var probe = Guid.NewGuid();
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        try
        {
            _ = metric switch
            {
                QuestionMetric.WeightKg => (object)WeightEntry.Create(probe, probe, day, v, null, now),
                QuestionMetric.BodyFatPercent => BodyFatEntry.Create(probe, probe, day, v, null, now),
                QuestionMetric.Steps => StepsEntry.Create(probe, probe, day, Whole(v, field), null, now),
                QuestionMetric.SleepHours => SleepEntry.Create(probe, probe, day, v, null, null, null, null, now),
                QuestionMetric.WaterMl => WaterEntry.Create(probe, probe, day, Whole(v, field), null, now),
                _ => BodyMeasurement.Create(probe, probe, day, SiteOf(metric), v, null, now)
            };
        }
        catch (ArgumentException exception)
        {
            throw Validation(field, exception.Message);
        }
    }

    /// <summary>
    /// Writes the measures a submitted form answered (P12) as the client's entries for <paramref name="date"/>: a
    /// weigh-in, body fat, measurements, steps and sleep hours replace the day's value; water adds a drink; photos
    /// become progress photos (the image stays shared with the answer).
    /// </summary>
    public async Task RecordMetricsAsync(Guid clientId, DateOnly date, IReadOnlyList<MetricAnswer> answers, CancellationToken cancellationToken)
    {
        if (answers.Count == 0) return;
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        foreach (var answer in answers)
        {
            switch (answer.Metric)
            {
                case QuestionMetric.WeightKg: await SetWeightAsync(clientId, date, new WeightInput(answer.Value!.Value), cancellationToken); break;
                case QuestionMetric.BodyFatPercent: await SetBodyFatAsync(clientId, date, new BodyFatInput(answer.Value!.Value), cancellationToken); break;
                case QuestionMetric.Steps: await SetStepsAsync(clientId, date, new StepsInput((int)answer.Value!.Value), cancellationToken); break;
                case QuestionMetric.WaterMl: await AddWaterAsync(clientId, new WaterInput(date, (int)answer.Value!.Value), cancellationToken); break;
                case QuestionMetric.SleepHours:
                    var night = (await repository.FindOnAsync<SleepEntry>(clientId, date, cancellationToken)).SingleOrDefault();
                    await SetSleepAsync(clientId, date, new SleepInput(answer.Value!.Value, night?.Quality), cancellationToken);
                    break;
                case QuestionMetric.FrontPhoto or QuestionMetric.SidePhoto or QuestionMetric.BackPhoto:
                    var pose = answer.Metric switch { QuestionMetric.FrontPhoto => PhotoPose.Front, QuestionMetric.SidePhoto => PhotoPose.Side, _ => PhotoPose.Back };
                    repository.Add(ProgressPhoto.Create(coachId, clientId, date, pose, null, answer.MediaId!.Value, "From a check-in", timeProvider.GetUtcNow()));
                    await repository.SaveChangesAsync(cancellationToken);
                    break;
                default:
                    await SetMeasurementsAsync(clientId, date, new MeasurementsInput([new(SiteOf(answer.Metric), answer.Value)]), cancellationToken);
                    break;
            }
        }
    }

    private static MeasurementSite SiteOf(QuestionMetric metric) => metric switch
    {
        QuestionMetric.WaistCm => MeasurementSite.Waist,
        QuestionMetric.ChestCm => MeasurementSite.Chest,
        QuestionMetric.ArmCm => MeasurementSite.Arm,
        QuestionMetric.ThighCm => MeasurementSite.Thigh,
        QuestionMetric.HipCm => MeasurementSite.Hip,
        QuestionMetric.NeckCm => MeasurementSite.Neck,
        QuestionMetric.CalfCm => MeasurementSite.Calf,
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    private static int Whole(decimal value, string field) =>
        decimal.Truncate(value) == value && value is >= int.MinValue and <= int.MaxValue ? (int)value : throw Validation(field, "Enter a whole number.");

    private async Task UpsertDailyAsync<T>(
        Guid clientId, DateOnly date, Func<Guid, DateTimeOffset, T> create, Action<T, DateTimeOffset> change, string field,
        CancellationToken cancellationToken) where T : ProgressEntry
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        var existing = (await repository.FindOnAsync<T>(clientId, date, cancellationToken)).SingleOrDefault();
        var now = timeProvider.GetUtcNow();
        DomainValidation.Run(() =>
        {
            if (existing is null) repository.Add(create(coachId, now));
            else change(existing, now);
        }, field);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteDailyAsync<T>(Guid clientId, DateOnly date, CancellationToken cancellationToken) where T : ProgressEntry
    {
        await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(date);
        var entries = await repository.FindOnAsync<T>(clientId, date, cancellationToken);
        if (entries.Count == 0) throw new NotFoundException(typeof(T).Name, date);
        foreach (var entry in entries) repository.Remove(entry);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<LifestyleTargetsResponse> TargetsForAsync(Guid clientId, Guid coachId, CancellationToken cancellationToken)
    {
        var own = (await repository.FindTargetsAsync(clientId, cancellationToken))?.Values ?? LifestyleTargetValues.None;
        var defaults = (await settings.CoachAsync(coachId, cancellationToken)).DefaultTargets;
        return new LifestyleTargetsResponse(own, defaults, own.Or(defaults));
    }

    private async Task<ProgressPhoto> RequiredPhotoAsync(Guid clientId, Guid id, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var photo = await repository.FindAsync<ProgressPhoto>(id, cancellationToken);
        return photo is not null && photo.ClientId == clientId ? photo : throw new NotFoundException("Progress photo", id);
    }

    private static SeriesResponse Series(IEnumerable<(SeriesEntry Entry, DateOnly Date)> rows, DateOnly from, DateOnly to, int decimals)
    {
        var all = rows.OrderBy(x => x.Date).ToArray();
        return new SeriesResponse(
            ProgressStatistics.Summarize(all.Select(x => new SeriesPoint(x.Date, x.Entry.Value)), from, to, decimals),
            all.Where(x => x.Date >= from && x.Date <= to).Select(x => x.Entry).ToArray());
    }

    private static SeriesEntry Entry(ProgressEntry entry, decimal value) => new(entry.Id, entry.Date, value, entry.Source, entry.Notes);
    private static SleepEntryResponse Map(SleepEntry x) => new(x.Id, x.Date, x.Hours, x.Quality, x.Bedtime, x.WakeTime, x.Source, x.Notes);
    private static StepsEntryResponse Map(StepsEntry x) => new(x.Id, x.Date, x.Steps, x.Source, x.Notes);
    private static WaterEntryResponse Map(WaterEntry x) => new(x.Id, x.Date, x.AmountMl, x.Source, x.Notes, x.CreatedAt);
    private static ProgressPhotoResponse Map(ProgressPhoto x) => new(x.Id, x.Date, x.Pose, x.Label, x.MediaId, x.Notes, x.CreatedAt);

    private async Task<Guid> ClientCoachAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientCoachIdAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private static void EnsureRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw Validation("to", "The end date cannot be before the start date.");
        if (to.DayNumber - from.DayNumber >= MaximumRangeDays) throw Validation("to", $"A range covers at most {MaximumRangeDays} days.");
    }

    /// <summary>Entries take days from a year back up to tomorrow (UTC), which covers every time zone's today.</summary>
    private void EnsureWritable(DateOnly date)
    {
        var today = Today();
        if (date > today.AddDays(1)) throw Validation("date", "An entry cannot be dated in the future.");
        if (date < today.AddDays(-MaximumRangeDays)) throw Validation("date", "The day is too far back to change.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
}
