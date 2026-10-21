using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Settings;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Training;
using CoachHub.Domain.WorkoutPlanning;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.WorkoutTracking;

/// <summary>
/// Workout tracking (§18), history (§19), PRs (§21, §22) and training analytics (§20, §23). Clients log their own
/// workouts against the plan in force; coaches read their clients'. Tenant filters decide who sees which client.
/// PRs are derived from completed workouts and rebuilt for the exercises a change touches.
/// </summary>
public sealed class WorkoutTrackingService(
    IWorkoutTrackingRepository repository, IWorkoutPlanRepository plans, SettingsService settings, TimeProvider timeProvider)
{
    /// <summary>The longest analytics range, and how far back workouts can be logged.</summary>
    public const int MaximumRangeDays = 366;
    public const int MaximumHistoryEntries = 200;
    public const int RecentRecords = 20;

    /// <summary>The training day: the workout in progress, the rotation of the plan in force (§18 "Today's Workout"), the week.</summary>
    public async Task<TrainingDayResponse> GetDayAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var weekStart = NutritionAdherence.WeekStart(date);
        var week = (await repository.ListSessionsAsync(clientId, weekStart, weekStart.AddDays(6), cancellationToken))
            .Where(x => x.IsCompleted).ToArray();
        var inForce = WorkoutSchedule.InForce(await repository.ListWorkoutVersionsAsync(clientId, cancellationToken), date);
        TrainingPlanToday? plan = null;
        if (inForce is not null && await plans.FindAsync(inForce.Plan.Id, inForce.Revision.VersionNumber, false, cancellationToken) is { } aggregate)
        {
            var done = week.Where(x => x.PlanId == inForce.Plan.Id && x.DayOrder.HasValue).Select(x => x.DayOrder!.Value).ToHashSet();
            var days = aggregate.Days.OrderBy(x => x.Order).ToArray();
            plan = new TrainingPlanToday(aggregate.Plan.Id, aggregate.Plan.NameEn, aggregate.Plan.NameAr, aggregate.Revision.VersionNumber,
                WorkoutSchedule.NextDay(days, done)?.Id,
                days.Select(day =>
                {
                    var exercises = aggregate.Exercises.Where(x => x.WorkoutDayId == day.Id).Select(x => x.Id).ToHashSet();
                    return new TrainingPlanDay(day.Id, day.NameEn, day.NameAr, day.Subtitle, day.Order, exercises.Count,
                        aggregate.Sets.Count(x => exercises.Contains(x.WorkoutExerciseId)), done.Contains(day.Order));
                }).ToArray());
        }
        var inProgress = await repository.FindInProgressAsync(clientId, cancellationToken);
        var sessions = (await repository.ListSessionsAsync(clientId, date, date, cancellationToken)).OrderBy(x => x.StartedAt);
        return new TrainingDayResponse(date, inProgress is null ? null : Summary(inProgress), plan,
            new TrainingWeekResponse(weekStart, inForce?.DayCount ?? 0, week.Length),
            sessions.Select(Summary).ToArray());
    }

    /// <summary>Starts a workout (§18 "Start Workout"): a day of the plan in force with its exercises, or a free workout.</summary>
    public async Task<WorkoutSessionResponse> StartAsync(Guid clientId, StartWorkoutInput input, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        EnsureWritable(input.Date);
        if (await repository.FindInProgressAsync(clientId, cancellationToken) is not null)
            throw new ConflictException("Finish or discard the workout in progress first.");
        var now = timeProvider.GetUtcNow();
        WorkoutSession session;
        if (input.WorkoutDayId is { } dayId)
        {
            var inForce = WorkoutSchedule.InForce(await repository.ListWorkoutVersionsAsync(clientId, cancellationToken), input.Date)
                ?? throw Validation("workoutDayId", "No workout plan applies to this day.");
            var aggregate = await plans.FindAsync(inForce.Plan.Id, inForce.Revision.VersionNumber, false, cancellationToken)
                ?? throw Validation("workoutDayId", "No workout plan applies to this day.");
            var day = aggregate.Days.SingleOrDefault(x => x.Id == dayId)
                ?? throw Validation("workoutDayId", "The day is not part of the plan in force.");
            var planned = WorkoutPlanService.MapDay(aggregate, day, await ExercisesOfAsync(aggregate, cancellationToken));
            session = DomainValidation.Run(() =>
            {
                var started = WorkoutSession.Start(coachId, clientId, input.Date, day.NameEn, day.NameAr,
                    new PlannedWorkoutReference(aggregate.Plan.Id, aggregate.Revision.Id, aggregate.Revision.VersionNumber, day.Id, day.Order), now);
                foreach (var exercise in planned.Exercises)
                    started.AddExercise(exercise.ExerciseId, exercise.Id, exercise.Block, exercise.GroupLabel, now);
                return started;
            }, "workout");
        }
        else
        {
            session = DomainValidation.Run(() => WorkoutSession.Start(
                coachId, clientId, input.Date, string.IsNullOrWhiteSpace(input.NameEn) ? "Workout" : input.NameEn, null, null, now), "nameEn");
        }
        await repository.AddAsync(session, cancellationToken);
        return await MapAsync(coachId, session, cancellationToken);
    }

    public async Task<WorkoutSessionResponse> GetSessionAsync(Guid clientId, Guid sessionId, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        return await MapAsync(coachId, await RequiredSessionAsync(clientId, sessionId, false, cancellationToken), cancellationToken);
    }

    /// <summary>The client's workouts, newest first.</summary>
    public async Task<PagedResult<WorkoutSessionSummary>> ListSessionsAsync(
        Guid clientId, WorkoutSessionQuery query, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var page = await repository.PageSessionsAsync(clientId, query.Normalize(), cancellationToken);
        return new PagedResult<WorkoutSessionSummary>(page.Items.Select(Summary).ToArray(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public Task<WorkoutSessionResponse> ChangeNotesAsync(
        Guid clientId, Guid sessionId, WorkoutNotesInput input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            DomainValidation.Run(() => session.ChangeNotes(input.Notes, now), "notes");
            return [];
        }, cancellationToken);

    /// <summary>Completes the workout (§18) and detects its PRs (§21).</summary>
    public Task<WorkoutSessionResponse> CompleteAsync(
        Guid clientId, Guid sessionId, WorkoutNotesInput? input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            if (session.IsCompleted) throw new ConflictException("The workout is already completed.");
            DomainValidation.Run(() =>
            {
                if (input is not null) session.ChangeNotes(input.Notes, now);
                session.Complete(now);
            }, "workout");
            return session.Exercises.Select(x => x.ExerciseId).ToArray();
        }, cancellationToken, recordsForCompleted: true);

    /// <summary>Discards a workout in progress, or deletes a completed one (its PRs are rebuilt without it).</summary>
    public async Task DeleteSessionAsync(Guid clientId, Guid sessionId, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var session = await RequiredSessionAsync(clientId, sessionId, true, cancellationToken);
        var exercises = session.IsCompleted ? session.Exercises.Select(x => x.ExerciseId).Distinct().ToArray() : [];
        repository.Remove(session);
        await repository.SaveChangesAsync(cancellationToken);
        if (exercises.Length > 0) await RebuildRecordsAsync(coachId, clientId, exercises, cancellationToken);
    }

    /// <summary>Adds an exercise from the catalog to the workout (an exercise the plan does not prescribe).</summary>
    public async Task<WorkoutSessionResponse> AddExerciseAsync(
        Guid clientId, Guid sessionId, AddLoggedExerciseInput input, CancellationToken cancellationToken)
    {
        var exercise = await repository.FindVisibleExerciseAsync(input.ExerciseId, cancellationToken);
        if (exercise is not { IsActive: true }) throw Validation("exerciseId", "Choose an active exercise from the library.");
        return await ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            DomainValidation.Run(() => session.AddExercise(exercise.Id, null, WorkoutBlock.Main, null, now), "exerciseId");
            return [];
        }, cancellationToken);
    }

    /// <summary>Removes an exercise the client added. Planned exercises are skipped instead.</summary>
    public Task<WorkoutSessionResponse> RemoveExerciseAsync(
        Guid clientId, Guid sessionId, Guid loggedExerciseId, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            var exercise = ExerciseOf(session, loggedExerciseId);
            DomainValidation.Run(() => session.RemoveExercise(exercise.Id, now), "exercise");
            return [exercise.ExerciseId];
        }, cancellationToken);

    /// <summary>Skips a planned exercise (§18), when the coach made every one of its sets skippable; or resumes it.</summary>
    public Task<WorkoutSessionResponse> SkipExerciseAsync(
        Guid clientId, Guid sessionId, Guid loggedExerciseId, SkipLoggedExerciseInput input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, planned, now) =>
        {
            var exercise = ExerciseOf(session, loggedExerciseId);
            if (input.IsSkipped && !CanSkip(Planned(planned, exercise)))
                throw Validation("isSkipped", "The coach does not permit skipping this exercise.");
            DomainValidation.Run(() => exercise.Skip(input.IsSkipped), "isSkipped");
            session.Touch(now);
            return [exercise.ExerciseId];
        }, cancellationToken);

    public Task<WorkoutSessionResponse> ChangeExerciseNotesAsync(
        Guid clientId, Guid sessionId, Guid loggedExerciseId, LoggedExerciseNotesInput input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            var exercise = ExerciseOf(session, loggedExerciseId);
            DomainValidation.Run(() => exercise.ChangeNotes(input.Notes), "notes");
            session.Touch(now);
            return [];
        }, cancellationToken);

    /// <summary>Logs a set (§18 "Log sets / reps / weight / RIR", "Complete Set"), or skips a skippable planned set.</summary>
    public Task<WorkoutSessionResponse> LogSetAsync(
        Guid clientId, Guid sessionId, Guid loggedExerciseId, LoggedSetInput input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, planned, now) =>
        {
            var exercise = ExerciseOf(session, loggedExerciseId);
            PrescribedSetResponse? prescribed = null;
            if (input.PlannedSetId is { } plannedSetId)
            {
                prescribed = Planned(planned, exercise)?.Sets.SingleOrDefault(x => x.Id == plannedSetId)
                    ?? throw Validation("plannedSetId", "The set is not prescribed for this exercise.");
            }
            EnsureSkipPermitted(input.Status, prescribed);
            DomainValidation.Run(() => exercise.LogSet(input.PlannedSetId, Values(input), now), "set");
            session.Touch(now);
            return [exercise.ExerciseId];
        }, cancellationToken);

    public Task<WorkoutSessionResponse> UpdateSetAsync(
        Guid clientId, Guid sessionId, Guid loggedSetId, LoggedSetInput input, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, planned, now) =>
        {
            var set = SetOf(session, loggedSetId);
            var exercise = session.ExerciseOf(set);
            var prescribed = set.PlannedSetId is { } plannedSetId ? Planned(planned, exercise)?.Sets.SingleOrDefault(x => x.Id == plannedSetId) : null;
            if (input.Status == LoggedSetStatus.Skipped && set.Status != LoggedSetStatus.Skipped) EnsureSkipPermitted(input.Status, prescribed);
            DomainValidation.Run(() => set.Change(Values(input), now), "set");
            session.Touch(now);
            return [exercise.ExerciseId];
        }, cancellationToken);

    public Task<WorkoutSessionResponse> DeleteSetAsync(
        Guid clientId, Guid sessionId, Guid loggedSetId, CancellationToken cancellationToken) =>
        ChangeAsync(clientId, sessionId, (session, _, now) =>
        {
            var set = SetOf(session, loggedSetId);
            var exercise = session.ExerciseOf(set);
            exercise.RemoveSet(set.Id);
            session.Touch(now);
            return [exercise.ExerciseId];
        }, cancellationToken);

    /// <summary>An exercise's history (§19), newest first, with its PR history (§21).</summary>
    public async Task<ExerciseHistoryResponse> HistoryAsync(Guid clientId, Guid exerciseId, CancellationToken cancellationToken)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var formula = await FormulaAsync(coachId, cancellationToken);
        var sessions = await repository.ListCompletedSessionsWithAsync(clientId, [exerciseId], cancellationToken);
        var records = await repository.ListRecordsAsync(clientId, [exerciseId], false, cancellationToken);
        var catalog = await repository.FindReferencedExercisesAsync([exerciseId], cancellationToken);
        if (!catalog.TryGetValue(exerciseId, out var exercise) ||
            (sessions.Count == 0 && await repository.FindVisibleExerciseAsync(exerciseId, cancellationToken) is null))
            throw new NotFoundException("Exercise", exerciseId);
        var entries = sessions.OrderByDescending(x => x.Date).ThenByDescending(x => x.CompletedAt).Take(MaximumHistoryEntries).Select(session =>
        {
            var sets = session.Exercises.Where(x => x.ExerciseId == exerciseId).OrderBy(x => x.Order)
                .SelectMany(x => x.Sets.OrderBy(set => set.Order)).ToArray();
            var figures = TrainingAnalytics.Figures(sets, formula);
            return new ExerciseHistoryEntry(session.Id, session.Date, figures.Sets, figures.Reps, figures.VolumeKg,
                figures.TopWeightKg, figures.TopWeightReps, figures.BestEstimatedOneRepMaxKg,
                sets.Select(x => Map(x, formula)).ToArray());
        }).ToArray();
        return new ExerciseHistoryResponse(exercise.Id, exercise.NameEn, exercise.NameAr, exercise.PrimaryMuscleGroupId, entries,
            records.OrderByDescending(x => x.AchievedOn).ThenBy(x => x.Type).ThenByDescending(x => x.Value)
                .Select(x => Map(x, catalog)).ToArray());
    }

    /// <summary>The current best of each exercise and record type, and the latest records (§21).</summary>
    public async Task<PersonalRecordsResponse> RecordsAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await ClientCoachAsync(clientId, cancellationToken);
        var records = await repository.ListRecordsAsync(clientId, null, false, cancellationToken);
        var catalog = await repository.FindReferencedExercisesAsync(records.Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);
        var current = records.GroupBy(x => (x.ExerciseId, x.Type)).Select(x => x.MaxBy(record => record.Value)!)
            .OrderBy(x => catalog.GetValueOrDefault(x.ExerciseId)?.NameEn).ThenBy(x => x.Type);
        var recent = records.OrderByDescending(x => x.AchievedOn).ThenBy(x => x.Type).Take(RecentRecords);
        return new PersonalRecordsResponse(current.Select(x => Map(x, catalog)).ToArray(), recent.Select(x => Map(x, catalog)).ToArray());
    }

    /// <summary>Training analytics (§20, §23): the range, each week (Monday first) and month, and each exercise's progression.</summary>
    public async Task<TrainingAnalyticsResponse> AnalyticsAsync(
        Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) throw Validation("to", "The end date cannot be before the start date.");
        if (to.DayNumber - from.DayNumber >= MaximumRangeDays) throw Validation("to", $"A range covers at most {MaximumRangeDays} days.");
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var coach = await settings.CoachAsync(coachId, cancellationToken);
        var formula = OneRepMax.For(coach.OneRepMaxFormula);
        var today = Today();
        var sessions = (await repository.ListSessionsAsync(clientId, from, to, cancellationToken)).Where(x => x.IsCompleted).ToArray();
        var versions = await repository.ListWorkoutVersionsAsync(clientId, cancellationToken);
        var perWeek = new Dictionary<DateOnly, int>();
        int DaysPerWeek(DateOnly day) =>
            perWeek.TryGetValue(day, out var count) ? count : perWeek[day] = WorkoutSchedule.InForce(versions, day)?.DayCount ?? 0;
        var catalog = await repository.FindReferencedExercisesAsync(
            sessions.SelectMany(x => x.Exercises).Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);
        var names = await repository.ListMuscleNamesAsync(cancellationToken);
        ExerciseMuscles Muscles(Guid id) => catalog.TryGetValue(id, out var exercise)
            ? new ExerciseMuscles(exercise.PrimaryMuscleGroupId, exercise.SecondaryMuscleGroupIds)
            : ExerciseMuscles.None;
        TrainingPeriodSummary Period(DateOnly start, DateOnly end) => TrainingAnalytics.Summarize(start, end, sessions, Muscles, names,
            coach.SecondaryMuscleWeight, WorkoutSchedule.Adherence(start, end, today, DaysPerWeek, sessions.Select(x => x.Date)));

        var last = to < today ? to : today;
        var weeks = new List<TrainingPeriodSummary>();
        for (var week = NutritionAdherence.WeekStart(from); week <= last; week = week.AddDays(7))
            weeks.Add(Period(Max(week, from), Min(week.AddDays(6), to)));
        var months = new List<TrainingPeriodSummary>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= last; month = month.AddMonths(1))
            months.Add(Period(Max(month, from), Min(month.AddMonths(1).AddDays(-1), to)));

        var exercises = sessions.SelectMany(session => session.Exercises.Select(exercise => (session, exercise)))
            .GroupBy(x => x.exercise.ExerciseId)
            .Select(group =>
            {
                var points = group.GroupBy(x => x.session).OrderBy(x => x.Key.Date).ThenBy(x => x.Key.CompletedAt).Select(x =>
                {
                    var figures = TrainingAnalytics.Figures(x.SelectMany(item => item.exercise.Sets), formula);
                    return new ExerciseProgressPoint(x.Key.Date, x.Key.Id, figures.TopWeightKg, figures.TopWeightReps, figures.MaxReps,
                        figures.BestEstimatedOneRepMaxKg, figures.VolumeKg, figures.Sets, figures.Reps);
                }).Where(x => x.Sets > 0).ToArray();
                var exercise = catalog.GetValueOrDefault(group.Key);
                return new ExerciseProgressResponse(group.Key, exercise?.NameEn ?? "Exercise", exercise?.NameAr, points);
            })
            .Where(x => x.Points.Count > 0)
            .OrderByDescending(x => x.Points.Count).ThenBy(x => x.NameEn)
            .ToArray();
        return new TrainingAnalyticsResponse(from, to, coach.OneRepMaxFormula, coach.SecondaryMuscleWeight, Period(from, to),
            weeks, months, exercises);
    }

    /// <summary>
    /// Loads the client's workout, applies a change and saves it. When the workout is completed (or the change
    /// completes it), the PRs of the exercises the change returns are rebuilt.
    /// </summary>
    private async Task<WorkoutSessionResponse> ChangeAsync(
        Guid clientId, Guid sessionId,
        Func<WorkoutSession, WorkoutDayResponse?, DateTimeOffset, IReadOnlyCollection<Guid>> change,
        CancellationToken cancellationToken, bool recordsForCompleted = false)
    {
        var coachId = await ClientCoachAsync(clientId, cancellationToken);
        var session = await RequiredSessionAsync(clientId, sessionId, true, cancellationToken);
        var planned = await PlannedDayAsync(session, cancellationToken);
        var wasCompleted = session.IsCompleted;
        var touched = change(session, planned, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        if ((wasCompleted || recordsForCompleted) && session.IsCompleted && touched.Count > 0)
            await RebuildRecordsAsync(coachId, clientId, touched.Distinct().ToArray(), cancellationToken);
        return await MapAsync(coachId, session, cancellationToken, planned);
    }

    /// <summary>
    /// Rebuilds the PRs of the exercises from all completed workouts (§21). Rows that stay the same are kept, so the
    /// audit trail shows only real changes.
    /// </summary>
    private async Task RebuildRecordsAsync(Guid coachId, Guid clientId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken)
    {
        var formula = await FormulaAsync(coachId, cancellationToken);
        var sessions = await repository.ListCompletedSessionsWithAsync(clientId, exerciseIds, cancellationToken);
        var existing = await repository.ListRecordsAsync(clientId, exerciseIds, true, cancellationToken);
        var desired = exerciseIds.SelectMany(exerciseId => PersonalRecordDetector.Detect(
                sessions.Select(session => new RecordSession(session.Id, session.Date, session.CompletedAt ?? session.UpdatedAt,
                        session.Exercises.Where(x => x.ExerciseId == exerciseId).SelectMany(x => x.Sets).ToArray()))
                    .Where(x => x.Sets.Count > 0), formula)
            .Select(record => PersonalRecord.Create(coachId, clientId, exerciseId, record.Type, record.Value, record.PreviousValue,
                record.WeightKg, record.Reps, record.SessionId, record.SetId, record.Date)))
            .ToArray();
        var kept = existing.Where(x => desired.Any(x.SameAs)).ToArray();
        repository.Remove(existing.Except(kept).ToArray());
        repository.Add(desired.Where(x => !kept.Any(x.SameAs)).ToArray());
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkoutSessionResponse> MapAsync(
        Guid coachId, WorkoutSession session, CancellationToken cancellationToken, WorkoutDayResponse? planned = null)
    {
        planned ??= await PlannedDayAsync(session, cancellationToken);
        var formula = await FormulaAsync(coachId, cancellationToken);
        var records = (await repository.ListRecordsAsync(session.ClientId, session.Exercises.Select(x => x.ExerciseId).Distinct().ToArray(), false, cancellationToken))
            .Where(x => x.WorkoutSessionId == session.Id).ToArray();
        var catalog = await repository.FindReferencedExercisesAsync(session.Exercises.Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);
        var exercises = session.Exercises.OrderBy(x => x.Order).Select(item =>
        {
            var exercise = catalog.GetValueOrDefault(item.ExerciseId);
            var prescription = Planned(planned, item);
            var group = prescription?.SetGroupId is { } groupId ? planned!.SetGroups.SingleOrDefault(x => x.Id == groupId) : null;
            return new LoggedExerciseResponse(item.Id, item.ExerciseId, exercise?.NameEn ?? "Exercise", exercise?.NameAr,
                exercise?.MediaId, exercise?.YouTubeUrl, item.PlannedExerciseId, item.Order, item.Block, item.GroupLabel,
                group?.RestSeconds, item.IsSkipped, CanSkip(prescription), item.Notes, prescription?.Notes,
                prescription?.Sets ?? [], item.Sets.OrderBy(x => x.Order).Select(x => Map(x, formula)).ToArray(),
                TrainingSets.Volume(item.Sets));
        }).ToArray();
        var summary = Summary(session);
        return new WorkoutSessionResponse(session.Id, session.Date, session.Status, session.NameEn, session.NameAr,
            session.PlanId, session.VersionNumber, session.WorkoutDayId, planned is not null, session.Notes,
            session.StartedAt, session.CompletedAt, summary.CompletedSets, summary.VolumeKg, exercises,
            records.OrderBy(x => x.Type).Select(x => Map(x, catalog)).ToArray());
    }

    private static WorkoutSessionSummary Summary(WorkoutSession session)
    {
        var sets = session.Exercises.SelectMany(x => x.Sets).ToArray();
        return new WorkoutSessionSummary(session.Id, session.Date, session.Status, session.NameEn, session.NameAr,
            session.PlanId, session.VersionNumber, session.WorkoutDayId, session.Exercises.Count,
            sets.Count(x => x.IsCompleted), TrainingSets.Volume(sets), session.StartedAt, session.CompletedAt);
    }

    private static LoggedSetResponse Map(LoggedSet set, IOneRepMaxFormula formula) => new(
        set.Id, set.PlannedSetId, set.Order, set.Type, set.Status, set.Reps, set.WeightKg, set.Rir, set.Rpe, set.DurationSeconds,
        set.Notes, set.VolumeKg,
        TrainingSets.CountsForSetRecords(set) && set.WeightKg is { } weight && set.Reps is { } reps ? formula.Estimate(weight, reps) : null,
        set.LoggedAt);

    private static PersonalRecordResponse Map(PersonalRecord record, IReadOnlyDictionary<Guid, Exercise> catalog)
    {
        var exercise = catalog.GetValueOrDefault(record.ExerciseId);
        return new PersonalRecordResponse(record.Id, record.ExerciseId, exercise?.NameEn ?? "Exercise", exercise?.NameAr, record.Type,
            record.Value, record.PreviousValue, record.WeightKg, record.Reps, record.WorkoutSessionId, record.AchievedOn);
    }

    private static LoggedSetValues Values(LoggedSetInput input) =>
        new(input.Type, input.Status, input.Reps, input.WeightKg, input.Rir, input.Rpe, input.DurationSeconds, input.Notes);

    /// <summary>The planned day a workout follows, as the builder shows it; null for a free workout or a deleted plan.</summary>
    private async Task<WorkoutDayResponse?> PlannedDayAsync(WorkoutSession session, CancellationToken cancellationToken)
    {
        if (session.PlanId is not { } planId || session.VersionNumber is not { } version) return null;
        var aggregate = await plans.FindAsync(planId, version, false, cancellationToken);
        var day = aggregate?.Days.SingleOrDefault(x => x.Id == session.WorkoutDayId);
        return day is null ? null : WorkoutPlanService.MapDay(aggregate!, day, await ExercisesOfAsync(aggregate!, cancellationToken));
    }

    // The plan is visible, so the exercises it references are readable as part of it.
    private Task<IReadOnlyDictionary<Guid, Exercise>> ExercisesOfAsync(WorkoutPlanAggregate aggregate, CancellationToken cancellationToken) =>
        plans.FindReferencedExercisesAsync(aggregate.Exercises.Select(x => x.ExerciseId).Distinct().ToArray(), cancellationToken);

    private static WorkoutExerciseResponse? Planned(WorkoutDayResponse? planned, LoggedExercise exercise) =>
        exercise.PlannedExerciseId is { } id ? planned?.Exercises.SingleOrDefault(x => x.Id == id) : null;

    /// <summary>§18 "skip an exercise if permitted": every prescribed set of the exercise is skippable.</summary>
    private static bool CanSkip(WorkoutExerciseResponse? planned) => planned is { Sets.Count: > 0 } && planned.Sets.All(x => x.IsSkippable);

    private static void EnsureSkipPermitted(LoggedSetStatus status, PrescribedSetResponse? prescribed)
    {
        if (status == LoggedSetStatus.Skipped && prescribed is not { IsSkippable: true })
            throw Validation("status", "The coach does not permit skipping this set.");
    }

    private static LoggedExercise ExerciseOf(WorkoutSession session, Guid loggedExerciseId) =>
        session.Exercises.SingleOrDefault(x => x.Id == loggedExerciseId) ?? throw new NotFoundException("Workout exercise", loggedExerciseId);

    private static LoggedSet SetOf(WorkoutSession session, Guid loggedSetId) =>
        session.Exercises.SelectMany(x => x.Sets).SingleOrDefault(x => x.Id == loggedSetId) ?? throw new NotFoundException("Workout set", loggedSetId);

    private async Task<IOneRepMaxFormula> FormulaAsync(Guid coachId, CancellationToken cancellationToken) =>
        OneRepMax.For((await settings.CoachAsync(coachId, cancellationToken)).OneRepMaxFormula);

    private async Task<Guid> ClientCoachAsync(Guid clientId, CancellationToken cancellationToken) =>
        await repository.FindClientCoachIdAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);

    private async Task<WorkoutSession> RequiredSessionAsync(Guid clientId, Guid sessionId, bool tracking, CancellationToken cancellationToken)
    {
        var session = await repository.FindSessionAsync(sessionId, tracking, cancellationToken);
        return session is not null && session.ClientId == clientId ? session : throw new NotFoundException("Workout", sessionId);
    }

    /// <summary>Workouts take days from a year back up to tomorrow (UTC), which covers every time zone's today.</summary>
    private void EnsureWritable(DateOnly date)
    {
        var today = Today();
        if (date > today.AddDays(1)) throw Validation("date", "A workout cannot be logged for a future day.");
        if (date < today.AddDays(-MaximumRangeDays)) throw Validation("date", "The day is too far back to log.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
    private static ValidationException Validation(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
}
