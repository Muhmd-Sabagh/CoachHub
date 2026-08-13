using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Media;
using CoachHub.Domain.Training;

namespace CoachHub.Application.Training;

public sealed class LegacyExerciseImportService(
    IExerciseRepository repository,
    IMediaRepository mediaRepository)
{
    public async Task<LegacyExerciseImportResult> ImportAsync(
        IReadOnlyCollection<LegacyExerciseImportRow> rows,
        CancellationToken cancellationToken)
    {
        var results = new List<LegacyExerciseImportRowResult>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = row.LegacyId > 0
                ? await repository.FindLegacyImportAsync(row.LegacyId, cancellationToken)
                : null;
            if (existing is not null)
            {
                results.Add(new(row.LegacyId, "AlreadyImported", existing.ExerciseId, []));
                continue;
            }

            var errors = await ValidateRowAsync(row, cancellationToken);
            if (errors.Count > 0)
            {
                results.Add(new(row.LegacyId, "Invalid", null, errors));
                continue;
            }

            var category = await repository.GetOrCreateUncategorizedAsync(cancellationToken);
            var input = new ExerciseInput(
                row.Name,
                null,
                category.Id,
                row.MediaId,
                row.YouTubeLink,
                true);
            var exercise = ExerciseService.Create(input);
            var record = LegacyExerciseImportRecord.Create(
                row.LegacyId,
                exercise.Id,
                DateTimeOffset.UtcNow);
            await repository.AddImportedAsync(exercise, record, cancellationToken);

            IReadOnlyList<string> messages =
                string.IsNullOrWhiteSpace(row.ImagePath) || row.MediaId.HasValue
                    ? Array.Empty<string>()
                    : ["Legacy image path was not retained. Upload the image through Media and update the exercise MediaId."];
            results.Add(new(row.LegacyId, "Imported", exercise.Id, messages));
        }

        return new LegacyExerciseImportResult(
            results.Count(result => result.Status == "Imported"),
            results.Count(result => result.Status == "AlreadyImported"),
            results.Count(result => result.Status == "Invalid"),
            results);
    }

    private async Task<IReadOnlyList<string>> ValidateRowAsync(
        LegacyExerciseImportRow row,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (row.LegacyId <= 0)
        {
            errors.Add("LegacyId must be positive.");
        }
        try
        {
            ExerciseService.Validate(new ExerciseInput(
                row.Name,
                null,
                Guid.NewGuid(),
                row.MediaId,
                row.YouTubeLink,
                true));
        }
        catch (ValidationException exception)
        {
            errors.AddRange(exception.Errors.SelectMany(pair => pair.Value));
        }

        if (row.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(row.MediaId.Value, cancellationToken);
            if (media is null || !media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("MediaId must reference an existing image uploaded through Media.");
            }
        }
        return errors;
    }
}