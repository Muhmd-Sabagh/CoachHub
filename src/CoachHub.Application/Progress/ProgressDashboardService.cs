using CoachHub.Application.Adherence;
using CoachHub.Application.CheckIns;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.WorkoutTracking;

namespace CoachHub.Application.Progress;

/// <summary>
/// The client progress dashboard (§37): body and lifestyle (P11), nutrition (P8), training (P10), and the check-in
/// section with adherence (P12), each from the service its detailed screen uses so the figures agree.
/// </summary>
public sealed class ProgressDashboardService(
    ProgressService progress,
    IProgressRepository repository,
    FoodTrackingService nutrition,
    WorkoutTrackingService training,
    CheckInService checkIns,
    AdherenceService adherence)
{
    public const int DashboardRecords = 5;

    public async Task<ProgressDashboardResponse> DashboardAsync(Guid clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var body = await progress.BodyAsync(clientId, from, to, cancellationToken);
        var photos = (await repository.ListPhotosAsync(clientId, null, cancellationToken)).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt).ToArray();
        var lifestyle = await progress.LifestyleAsync(clientId, from, to, cancellationToken);
        var food = await nutrition.AnalyticsAsync(clientId, from, to, cancellationToken);
        var workouts = await training.AnalyticsAsync(clientId, from, to, cancellationToken);
        var records = await training.RecordsAsync(clientId, cancellationToken);
        var checkInAdherence = await checkIns.AdherenceAsync(clientId, from, to, cancellationToken);
        var scores = await adherence.ScoreAsync(clientId, from, to, cancellationToken);
        var mine = await checkIns.MineAsync(clientId, cancellationToken);
        var feedback = mine.History.SelectMany(x => x.Replies).OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return new ProgressDashboardResponse(from, to,
            new BodyDashboard(body.Weight.Summary, body.BodyFat.Summary,
                body.Measurements.Where(x => x.Summary.Count > 0).Select(x => new MeasurementChange(x.Site, x.Summary)).ToArray(),
                photos.Length, photos.Length == 0 ? null : Map(photos[0]), photos.Length == 0 ? null : Map(photos[^1])),
            food.Summary,
            new TrainingDashboard(workouts.Summary, records.Recent.Take(DashboardRecords).ToArray(),
                records.Current.Where(x => x.Type == PersonalRecordType.EstimatedOneRepMax)
                    .OrderByDescending(x => x.Value).Take(DashboardRecords).ToArray()),
            lifestyle.Summary, lifestyle.Targets,
            new CheckInDashboard(mine.History.FirstOrDefault(), mine.Schedule, checkInAdherence, feedback),
            scores);
    }

    private static ProgressPhotoResponse Map(Domain.Progress.ProgressPhoto x) => new(x.Id, x.Date, x.Pose, x.Label, x.MediaId, x.Notes, x.CreatedAt);
}
