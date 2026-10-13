using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Training;

namespace CoachHub.Application.Training;

/// <summary>
/// The exercise library (§17): platform exercises managed by administrators, plus each coach's custom
/// exercises. Searches cover both; the query filter hides other coaches' custom exercises.
/// </summary>
public sealed class ExerciseService(
    IExerciseRepository repository,
    IReferenceRepository<ExerciseCategory> categories,
    IReferenceRepository<MuscleGroup> muscleGroups,
    IReferenceRepository<Equipment> equipment,
    IMediaRepository mediaRepository,
    CoachOwnership ownership,
    ICurrentUser currentUser)
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

    public async Task<ExerciseResponse> CreateAsync(ExerciseInput input, CancellationToken cancellationToken)
    {
        Validate(input, requirePrimaryMuscle: true);
        // An administrator creates platform exercises unless they name a coach; a coach creates their own.
        Guid? owner = currentUser.IsAdministrator && input.CoachId is null
            ? null
            : await ownership.ForNewRecordAsync(input.CoachId, cancellationToken);
        await ValidateReferencesAsync(input, owner, cancellationToken);
        var exercise = DomainValidation.Run(() => Exercise.Create(owner, Values(input)), "exercise");
        await repository.AddAsync(exercise, cancellationToken);
        return Map(exercise);
    }

    public async Task<ExerciseResponse> UpdateAsync(
        Guid id,
        ExerciseInput input,
        CancellationToken cancellationToken)
    {
        Validate(input, requirePrimaryMuscle: true);
        var exercise = await FindWritableAsync(id, cancellationToken);
        await ValidateReferencesAsync(input, exercise.CoachId, cancellationToken);
        DomainValidation.Run(() => exercise.Update(Values(input)), "exercise");
        await repository.SaveChangesAsync(cancellationToken);
        return Map(exercise);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await FindWritableAsync(id, cancellationToken);
        await repository.DeleteAsync(exercise, cancellationToken);
    }

    internal static void Validate(ExerciseInput input, bool requirePrimaryMuscle = false)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.NameEn))
            errors["nameEn"] = ["An English name is required."];
        else if (input.NameEn.Trim().Length > 255)
            errors["nameEn"] = ["The English name cannot exceed 255 characters."];
        if (!string.IsNullOrWhiteSpace(input.NameAr) && input.NameAr.Trim().Length > 255)
            errors["nameAr"] = ["The Arabic name cannot exceed 255 characters."];
        if (input.ExerciseCategoryId == Guid.Empty)
            errors["exerciseCategoryId"] = ["An exercise category is required."];
        if (!string.IsNullOrWhiteSpace(input.YouTubeUrl) &&
            (input.YouTubeUrl.Trim().Length > 500 || !Exercise.IsValidYouTubeUrl(input.YouTubeUrl)))
            errors["youTubeUrl"] = ["Supply a valid HTTPS YouTube URL."];
        if (requirePrimaryMuscle && (input.PrimaryMuscleGroupId is null || input.PrimaryMuscleGroupId == Guid.Empty))
            errors["primaryMuscleGroupId"] = ["A primary muscle group is required."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    internal static ExerciseValues Values(ExerciseInput input) => new(
        input.NameEn, input.NameAr, input.ExerciseCategoryId, input.MediaId, input.YouTubeUrl, input.IsActive,
        input.Difficulty, input.PrimaryMuscleGroupId, input.SecondaryMuscleGroupIds, input.EquipmentIds,
        input.InstructionsEn, input.InstructionsAr, input.CoachingCuesEn, input.CoachingCuesAr,
        input.CommonMistakesEn, input.CommonMistakesAr);

    internal static ExerciseResponse Map(Exercise exercise) => new(
        exercise.Id,
        exercise.NameEn,
        exercise.NameAr,
        exercise.ExerciseCategoryId,
        exercise.MediaId,
        exercise.YouTubeUrl,
        exercise.IsActive,
        exercise.CoachId,
        !exercise.IsPlatform,
        exercise.Difficulty,
        exercise.PrimaryMuscleGroupId,
        exercise.SecondaryMuscleGroupIds,
        exercise.EquipmentIds,
        exercise.InstructionsEn,
        exercise.InstructionsAr,
        exercise.CoachingCuesEn,
        exercise.CoachingCuesAr,
        exercise.CommonMistakesEn,
        exercise.CommonMistakesAr);

    private async Task ValidateReferencesAsync(ExerciseInput input, Guid? owner, CancellationToken cancellationToken)
    {
        if (await categories.FindAsync(input.ExerciseCategoryId, cancellationToken) is null)
            throw Validation("exerciseCategoryId", "The selected exercise category does not exist.");
        Guid[] muscles = [.. input.PrimaryMuscleGroupId is { } primary ? [primary] : Array.Empty<Guid>(), .. input.SecondaryMuscleGroupIds ?? []];
        foreach (var id in muscles.Distinct())
            if (await muscleGroups.FindAsync(id, cancellationToken) is null)
                throw Validation("muscleGroupIds", "A selected muscle group does not exist.");
        foreach (var id in (input.EquipmentIds ?? []).Distinct())
            if (await equipment.FindAsync(id, cancellationToken) is null)
                throw Validation("equipmentIds", "A selected piece of equipment does not exist.");
        if (input.MediaId.HasValue)
        {
            var media = await mediaRepository.FindAsync(input.MediaId.Value, cancellationToken);
            // Platform exercises show platform images only; a custom exercise may also use its coach's library.
            var allowed = MediaService.IsPlatformImage(media) ||
                (owner is { } coachId && MediaService.IsCoachLibraryImage(media, coachId));
            if (!allowed)
                throw Validation("mediaId", owner is null
                    ? "The selected media must be an existing platform image."
                    : "The selected media must be a platform image or an image in the coach's library.");
        }
    }

    /// <summary>A visible exercise the caller may change: administrators any, a coach only their own.</summary>
    private async Task<Exercise> FindWritableAsync(Guid id, CancellationToken cancellationToken)
    {
        var exercise = await FindRequiredAsync(id, cancellationToken);
        if (!currentUser.IsAdministrator && exercise.CoachId != currentUser.CoachId)
            throw new ForbiddenException("Platform exercises are managed by administrators. Create a custom exercise instead.");
        return exercise;
    }

    private async Task<Exercise> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException("Exercise", id);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
