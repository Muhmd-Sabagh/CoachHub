using System.Text.Json.Serialization;
using CoachHub.Domain.Common;
using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Clients;

[JsonConverter(typeof(JsonStringEnumConverter<Sex>))]
public enum Sex
{
    Male,
    Female
}

/// <summary>The client's main goal (§3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FitnessGoal>))]
public enum FitnessGoal
{
    LoseFat,
    BuildMuscle,
    Recomposition,
    Maintain,
    Performance,
    GeneralHealth
}

[JsonConverter(typeof(JsonStringEnumConverter<TrainingLocation>))]
public enum TrainingLocation
{
    Gym,
    Home,
    Outdoor,
    Mixed
}

/// <summary>Dietary restrictions (§3), typed so food choices can be filtered later; notes go alongside.</summary>
[Flags]
public enum DietaryRestrictions
{
    None = 0,
    Vegetarian = 1,
    Vegan = 2,
    GlutenFree = 4,
    LactoseFree = 8,
    NutFree = 16,
    Halal = 32,
    Pescatarian = 64,
    LowSodium = 128,
    Diabetic = 256
}

[JsonConverter(typeof(JsonStringEnumConverter<FoodPreferenceKind>))]
public enum FoodPreferenceKind
{
    Preferred,
    Disliked
}

/// <summary>A catalog food the client prefers or dislikes.</summary>
public sealed class ClientFoodPreference : Entity
{
    private ClientFoodPreference() { }

    public Guid ClientProfileId { get; private set; }
    public Guid FoodItemId { get; private set; }
    public FoodPreferenceKind Kind { get; private set; }

    internal static ClientFoodPreference Create(Guid profileId, Guid foodItemId, FoodPreferenceKind kind) =>
        new() { ClientProfileId = profileId, FoodItemId = foodItemId, Kind = kind };
}

/// <summary>A piece of equipment the client has access to.</summary>
public sealed class ClientEquipment : Entity
{
    private ClientEquipment() { }

    public Guid ClientProfileId { get; private set; }
    public Guid EquipmentId { get; private set; }

    internal static ClientEquipment Create(Guid profileId, Guid equipmentId) =>
        new() { ClientProfileId = profileId, EquipmentId = equipmentId };
}

/// <summary>
/// The structured client profile (§3, D3): what the coach enters at onboarding and edits later.
/// Typed fields feed the calculations (age, sex, height and weight for BMR; activity level for
/// the coach's factor). Free-text notes sit alongside, never instead. Reached through its client.
/// </summary>
public sealed class ClientProfile : Entity
{
    public const int MaxNoteLength = 2000;

    private readonly List<ClientEquipment> _equipment = [];
    private readonly List<ClientFoodPreference> _foods = [];

    private ClientProfile() { }

    public Guid ClientId { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public Sex? Sex { get; private set; }
    public decimal? HeightCm { get; private set; }

    /// <summary>The weight at onboarding; it seeds the weight history (P11).</summary>
    public decimal? StartingWeightKg { get; private set; }
    public decimal? StartingBodyFatPercent { get; private set; }
    public FitnessGoal? Goal { get; private set; }
    public ActivityLevel? ActivityLevel { get; private set; }
    public int? TrainingDaysPerWeek { get; private set; }
    public int? AverageDailySteps { get; private set; }
    public TrainingLocation? TrainingLocation { get; private set; }
    public string? TrainingPreferences { get; private set; }
    public string? FoodPreferences { get; private set; }
    public string? PreferredFoodsNote { get; private set; }
    public string? DislikedFoodsNote { get; private set; }
    public DietaryRestrictions DietaryRestrictions { get; private set; }
    public string? DietaryNotes { get; private set; }
    public int? PreferredMealCount { get; private set; }
    public TimeOnly? Bedtime { get; private set; }
    public TimeOnly? WakeTime { get; private set; }
    public string? LifestyleNotes { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ClientEquipment> Equipment => _equipment;
    public IReadOnlyCollection<ClientFoodPreference> Foods => _foods;

    public static ClientProfile Create(Guid clientId, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        return new ClientProfile { ClientId = clientId, UpdatedAt = now };
    }

    /// <summary>The age in whole years on <paramref name="today"/>; null without a date of birth.</summary>
    public int? AgeOn(DateOnly today)
    {
        if (DateOfBirth is not { } birth) return null;
        var age = today.Year - birth.Year;
        return birth.AddYears(age) > today ? age - 1 : age;
    }

    public void Update(ClientProfileValues values, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.DateOfBirth is { } birth && (birth > today.AddYears(-10) || birth < today.AddYears(-110)))
            throw new ArgumentException("The client must be between 10 and 110 years old.", "dateOfBirth");
        Defined(values.Sex, "sex");
        Defined(values.Goal, "goal");
        Defined(values.ActivityLevel, "activityLevel");
        Defined(values.TrainingLocation, "trainingLocation");
        if ((values.DietaryRestrictions & ~AllRestrictions) != 0)
            throw new ArgumentException("Unknown dietary restriction.", "dietaryRestrictions");

        DateOfBirth = values.DateOfBirth;
        Sex = values.Sex;
        HeightCm = Range(values.HeightCm, 50m, 260m, "heightCm");
        StartingWeightKg = Range(values.StartingWeightKg, 20m, 400m, "startingWeightKg");
        StartingBodyFatPercent = Range(values.StartingBodyFatPercent, 2m, 70m, "startingBodyFatPercent");
        Goal = values.Goal;
        ActivityLevel = values.ActivityLevel;
        TrainingDaysPerWeek = Range(values.TrainingDaysPerWeek, 0, 7, "trainingDaysPerWeek");
        AverageDailySteps = Range(values.AverageDailySteps, 0, 100_000, "averageDailySteps");
        TrainingLocation = values.TrainingLocation;
        TrainingPreferences = Note(values.TrainingPreferences, "trainingPreferences");
        FoodPreferences = Note(values.FoodPreferences, "foodPreferences");
        PreferredFoodsNote = Note(values.PreferredFoodsNote, "preferredFoodsNote");
        DislikedFoodsNote = Note(values.DislikedFoodsNote, "dislikedFoodsNote");
        DietaryRestrictions = values.DietaryRestrictions;
        DietaryNotes = Note(values.DietaryNotes, "dietaryNotes");
        PreferredMealCount = Range(values.PreferredMealCount, 1, 10, "preferredMealCount");
        Bedtime = values.Bedtime;
        WakeTime = values.WakeTime;
        LifestyleNotes = Note(values.LifestyleNotes, "lifestyleNotes");

        var equipment = values.EquipmentIds.Distinct().ToArray();
        if (equipment.Any(id => id == Guid.Empty)) throw new ArgumentException("Unknown equipment.", "equipmentIds");
        var preferred = values.PreferredFoodIds.Distinct().ToArray();
        var disliked = values.DislikedFoodIds.Distinct().ToArray();
        if (preferred.Intersect(disliked).Any())
            throw new ArgumentException("A food cannot be both preferred and disliked.", "dislikedFoodIds");
        if (preferred.Concat(disliked).Any(id => id == Guid.Empty)) throw new ArgumentException("Unknown food.", "preferredFoodIds");

        _equipment.RemoveAll(item => !equipment.Contains(item.EquipmentId));
        foreach (var id in equipment.Where(id => _equipment.All(item => item.EquipmentId != id)))
            _equipment.Add(ClientEquipment.Create(Id, id));
        Sync(FoodPreferenceKind.Preferred, preferred);
        Sync(FoodPreferenceKind.Disliked, disliked);
        UpdatedAt = now;
    }

    private void Sync(FoodPreferenceKind kind, Guid[] foodIds)
    {
        _foods.RemoveAll(item => item.Kind == kind && !foodIds.Contains(item.FoodItemId));
        foreach (var id in foodIds.Where(id => _foods.All(item => item.Kind != kind || item.FoodItemId != id)))
            _foods.Add(ClientFoodPreference.Create(Id, id, kind));
    }

    private static readonly DietaryRestrictions AllRestrictions =
        Enum.GetValues<DietaryRestrictions>().Aggregate(DietaryRestrictions.None, (all, flag) => all | flag);

    private static void Defined<T>(T? value, string name) where T : struct, Enum
    {
        if (value is { } item && !Enum.IsDefined(item)) throw new ArgumentException("Unknown value.", name);
    }

    private static decimal? Range(decimal? value, decimal min, decimal max, string name) =>
        value is null || (value >= min && value <= max) ? value : throw new ArgumentException($"Must be between {min} and {max}.", name);

    private static int? Range(int? value, int min, int max, string name) =>
        value is null || (value >= min && value <= max) ? value : throw new ArgumentException($"Must be between {min} and {max}.", name);

    private static string? Note(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : throw new ArgumentException($"At most {MaxNoteLength} characters.", name);
    }
}

/// <summary>Everything a coach sets on a profile at once.</summary>
public sealed record ClientProfileValues(
    DateOnly? DateOfBirth,
    Sex? Sex,
    decimal? HeightCm,
    decimal? StartingWeightKg,
    decimal? StartingBodyFatPercent,
    FitnessGoal? Goal,
    ActivityLevel? ActivityLevel,
    int? TrainingDaysPerWeek,
    int? AverageDailySteps,
    TrainingLocation? TrainingLocation,
    string? TrainingPreferences,
    string? FoodPreferences,
    IReadOnlyCollection<Guid> PreferredFoodIds,
    string? PreferredFoodsNote,
    IReadOnlyCollection<Guid> DislikedFoodIds,
    string? DislikedFoodsNote,
    DietaryRestrictions DietaryRestrictions,
    string? DietaryNotes,
    IReadOnlyCollection<Guid> EquipmentIds,
    int? PreferredMealCount,
    TimeOnly? Bedtime,
    TimeOnly? WakeTime,
    string? LifestyleNotes);
