using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;

namespace CoachHub.Application.Training;

public sealed class ExerciseService(
    IExerciseRepository repository,
    IReferenceRepository<ExerciseCategory> categories,
    IMediaRepository mediaRepository)
{
    public async Task<PagedResult<ExerciseResponse>> ListAsync(
        ExerciseQuery query,
        CancellationToken cancellationToken)
    {
        var page = await repository.ListAsync(query.Normalize(), cancellationToken);
        return new PagedResult<ExerciseResponse>(
            page.Items.Select(Map).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<ExerciseResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await FindRequiredAsync(id, cancellationToken));

    public async Task<ExerciseResponse> CreateAsync(
        ExerciseInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(input, cancellationToken);
        var exercise = Create(input);
        await repository.AddAsync(exercise, cancellationToken);
        return Map(exercise);
    }

    public async Task<ExerciseResponse> UpdateAsync(
        Guid id,
        ExerciseInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        await ValidateReferencesAsync(input, cancellationToken);
        var exercise = await FindRequiredAsync(id, cancellationToken);
        exercise.Update(
            input.NameEn,
            input.NameAr,
            input.ExerciseCategoryId,
            input.MediaId,
            input.YouTubeUrl,
            input.IsActive);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(exercise);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await FindRequiredAsync(id, cancellationToken);
        await repository.DeleteAsync(exercise, cancellationToken);
    }

    internal static void Validate(ExerciseInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.NameEn))
        {
            errors["nameEn"] = ["An English name is required."];
        }
        else if (input.NameEn.Trim().Length > 255)
        {
            errors["nameEn"] = ["The English name cannot exceed 255 characters."];
        }
        if (!string.IsNullOrWhiteSpace(input.NameAr) && input.NameAr.Trim().Length > 255)
        {
            errors["nameAr"] = ["The Arabic name cannot exceed 255 characters."];
        }
        if (input.ExerciseCategoryId == Guid.Empty)
        {
            errors["exerciseCategoryId"] = ["An exercise category is required."];
        }
        if (!string.IsNullOrWhiteSpace(input.YouTubeUrl) &&
            (input.YouTubeUrl.Trim().Length > 500 || !Exercise.IsValidYouTubeUrl(input.YouTubeUrl)))
        {
            errors["youTubeUrl"] = ["Supply a valid HTTPS YouTube URL."];
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    internal static Exercise Create(ExerciseInput input) => Exercise.Create(
        input.NameEn,
        input.NameAr,
        input.ExerciseCategoryId,
        input.MediaId,
        input.YouTubeUrl,
        input.IsActive);

    internal static ExerciseResponse Map(Exercise exercise) => new(
        exercise.Id,
        exercise.NameEn,
        exercise.NameAr,
        exercise.ExerciseCategoryId,
        exercise.MediaId,
        exercise.YouTubeUrl,
        exercise.IsActive);

    private async Task ValidateReferencesAsync(
        ExerciseInput input,
        CancellationToken cancellationToken)
    {
        if (await categories.FindAsync(input.ExerciseCategoryId, cancellationToken) is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["exerciseCategoryId"] = ["The selected exercise category does not exist."]
            });
        }
        if (input.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(input.MediaId.Value, cancellationToken);
            if (media is null || !media.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["mediaId"] = ["The selected media must be an existing image."]
                });
            }
        }
    }

    private async Task<Exercise> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Exercise", id);
}