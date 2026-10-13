using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Training;

[JsonConverter(typeof(JsonStringEnumConverter<ExerciseDifficulty>))]
public enum ExerciseDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

/// <summary>Everything set on an exercise at once (§17). Long texts are optional and bilingual.</summary>
public sealed record ExerciseValues(
    string NameEn,
    string? NameAr,
    Guid ExerciseCategoryId,
    Guid? MediaId = null,
    string? YouTubeUrl = null,
    bool IsActive = true,
    ExerciseDifficulty? Difficulty = null,
    Guid? PrimaryMuscleGroupId = null,
    IReadOnlyCollection<Guid>? SecondaryMuscleGroupIds = null,
    IReadOnlyCollection<Guid>? EquipmentIds = null,
    string? InstructionsEn = null,
    string? InstructionsAr = null,
    string? CoachingCuesEn = null,
    string? CoachingCuesAr = null,
    string? CommonMistakesEn = null,
    string? CommonMistakesAr = null);

/// <summary>
/// An exercise of the library (§17): its muscles, equipment, difficulty, media and coaching texts. A catalog
/// entry: platform exercises (no coach) are managed by administrators; a coach keeps custom exercises.
/// </summary>
public sealed class Exercise : Entity, ICatalogEntry
{
    public const int MaximumTextLength = 4000;
    public const int MaximumLinks = 20;

    private readonly List<ExerciseSecondaryMuscle> _secondaryMuscles = [];
    private readonly List<ExerciseEquipment> _equipment = [];

    private Exercise() { }

    public Guid? CoachId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid ExerciseCategoryId { get; private set; }
    /// <summary>The exercise's image.</summary>
    public Guid? MediaId { get; private set; }
    public string? YouTubeUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ExerciseDifficulty? Difficulty { get; private set; }
    /// <summary>The muscle the exercise mainly trains; null only for exercises imported without one.</summary>
    public Guid? PrimaryMuscleGroupId { get; private set; }
    public string? InstructionsEn { get; private set; }
    public string? InstructionsAr { get; private set; }
    public string? CoachingCuesEn { get; private set; }
    public string? CoachingCuesAr { get; private set; }
    public string? CommonMistakesEn { get; private set; }
    public string? CommonMistakesAr { get; private set; }
    public IReadOnlyCollection<ExerciseSecondaryMuscle> SecondaryMuscles => _secondaryMuscles;
    public IReadOnlyCollection<ExerciseEquipment> Equipment => _equipment;
    public bool IsPlatform => CoachId is null;

    public static Exercise Create(Guid? coachId, ExerciseValues values)
    {
        var exercise = new Exercise { CoachId = TenantKey.Optional(coachId) };
        exercise.Update(values);
        return exercise;
    }

    public void Update(ExerciseValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        NameEn = Required(values.NameEn, 255, "nameEn");
        NameAr = Optional(values.NameAr, 255, "nameAr");
        if (values.ExerciseCategoryId == Guid.Empty)
            throw new ArgumentException("An exercise category is required.", "exerciseCategoryId");
        ExerciseCategoryId = values.ExerciseCategoryId;
        MediaId = values.MediaId;
        YouTubeUrl = NormalizeYouTubeUrl(values.YouTubeUrl);
        IsActive = values.IsActive;
        if (values.Difficulty is { } difficulty && !Enum.IsDefined(difficulty))
            throw new ArgumentException("Unknown difficulty.", "difficulty");
        Difficulty = values.Difficulty;
        if (values.PrimaryMuscleGroupId == Guid.Empty) throw new ArgumentException("Muscle group identifier cannot be empty.", "primaryMuscleGroupId");
        PrimaryMuscleGroupId = values.PrimaryMuscleGroupId;
        InstructionsEn = Optional(values.InstructionsEn, MaximumTextLength, "instructionsEn");
        InstructionsAr = Optional(values.InstructionsAr, MaximumTextLength, "instructionsAr");
        CoachingCuesEn = Optional(values.CoachingCuesEn, MaximumTextLength, "coachingCuesEn");
        CoachingCuesAr = Optional(values.CoachingCuesAr, MaximumTextLength, "coachingCuesAr");
        CommonMistakesEn = Optional(values.CommonMistakesEn, MaximumTextLength, "commonMistakesEn");
        CommonMistakesAr = Optional(values.CommonMistakesAr, MaximumTextLength, "commonMistakesAr");

        var secondary = Links(values.SecondaryMuscleGroupIds, "secondaryMuscleGroupIds");
        if (PrimaryMuscleGroupId is { } primary && secondary.Contains(primary))
            throw new ArgumentException("The primary muscle group cannot also be secondary.", "secondaryMuscleGroupIds");
        Sync(_secondaryMuscles, secondary, x => x.MuscleGroupId, id => ExerciseSecondaryMuscle.Create(Id, id));
        Sync(_equipment, Links(values.EquipmentIds, "equipmentIds"), x => x.EquipmentId, id => ExerciseEquipment.Create(Id, id));
    }

    public IReadOnlyList<Guid> SecondaryMuscleGroupIds => _secondaryMuscles.Select(x => x.MuscleGroupId).ToArray();
    public IReadOnlyList<Guid> EquipmentIds => _equipment.Select(x => x.EquipmentId).ToArray();

    public static bool IsValidYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.IdnHost;
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<Guid> Links(IReadOnlyCollection<Guid>? ids, string name)
    {
        var values = ids ?? [];
        if (values.Any(x => x == Guid.Empty)) throw new ArgumentException("Identifiers cannot be empty.", name);
        var set = values.ToHashSet();
        if (set.Count != values.Count) throw new ArgumentException("Identifiers must be unique.", name);
        if (set.Count > MaximumLinks) throw new ArgumentException($"At most {MaximumLinks}.", name);
        return set;
    }

    private static void Sync<T>(List<T> rows, HashSet<Guid> wanted, Func<T, Guid> key, Func<Guid, T> create)
    {
        rows.RemoveAll(row => !wanted.Contains(key(row)));
        foreach (var id in wanted.Where(id => rows.All(row => key(row) != id))) rows.Add(create(id));
    }

    private static string? NormalizeYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 500 || !IsValidYouTubeUrl(normalized))
            throw new ArgumentException("A valid HTTPS YouTube URL is required.", "youTubeUrl");
        return normalized;
    }

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximumLength, parameterName);
}

/// <summary>A muscle group the exercise also trains (§17, §23).</summary>
public sealed class ExerciseSecondaryMuscle : Entity
{
    private ExerciseSecondaryMuscle() { }
    public Guid ExerciseId { get; private set; }
    public Guid MuscleGroupId { get; private set; }
    internal static ExerciseSecondaryMuscle Create(Guid exerciseId, Guid muscleGroupId) =>
        new() { ExerciseId = exerciseId, MuscleGroupId = muscleGroupId };
}

/// <summary>Equipment the exercise uses (§17).</summary>
public sealed class ExerciseEquipment : Entity
{
    private ExerciseEquipment() { }
    public Guid ExerciseId { get; private set; }
    public Guid EquipmentId { get; private set; }
    internal static ExerciseEquipment Create(Guid exerciseId, Guid equipmentId) =>
        new() { ExerciseId = exerciseId, EquipmentId = equipmentId };
}
