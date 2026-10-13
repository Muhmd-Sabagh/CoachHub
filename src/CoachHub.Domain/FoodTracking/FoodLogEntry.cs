using System.Text.Json.Serialization;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;

namespace CoachHub.Domain.FoodTracking;

/// <summary>Where a diary entry came from (§8, §9).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FoodLogSource>))]
public enum FoodLogSource
{
    /// <summary>A food of the plan, eaten as planned.</summary>
    Planned,

    /// <summary>An alternative the coach permitted, eaten instead of a planned food (§7).</summary>
    Swap,

    /// <summary>Food outside the plan: a catalog food or a custom entry.</summary>
    Extra
}

/// <summary>The nutrients of an entry as eaten, fixed when it is logged (D3).</summary>
public sealed record FoodLogNutrients(decimal Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber = 0)
{
    public FoodLogNutrients Validate()
    {
        Range(Calories, 20000m, "calories");
        Range(Protein, 2000m, "protein");
        Range(Carbohydrates, 2000m, "carbohydrates");
        Range(Fat, 1000m, "fat");
        Range(Fiber, 1000m, "fiber");
        return this;
    }

    private static void Range(decimal value, decimal maximum, string name)
    {
        if (value < 0 || value > maximum) throw new ArgumentException($"Must be between 0 and {maximum}.", name);
    }
}

/// <summary>
/// A catalog food as eaten: <paramref name="Quantity"/> in <paramref name="FoodUnitId"/> (or the base unit),
/// worth <paramref name="BaseAmount"/> base units, with the unit's label at the time.
/// </summary>
public sealed record FoodLogPortion(
    Guid FoodItemId, Guid? FoodUnitId, decimal Quantity, decimal BaseAmount, string UnitLabel, string? UnitLabelAr);

/// <summary>
/// The planned food an entry stands for: a row of a planned meal, and for a swap the alternative eaten
/// instead (§7). The identifiers point at plan version rows, which may later be archived or deleted, so
/// they are references only; the entry keeps its own name and nutrients.
/// </summary>
public sealed record PlannedFoodReference(Guid MealId, Guid MealFoodItemId, Guid? ReplacementOptionId = null);

/// <summary>
/// One line of the client's food diary (§8): what was eaten, when (date and meal slot), how much, and its
/// nutrients at the time. Actual nutrition is kept apart from the plan (§9): a planned row is only referenced.
/// </summary>
public sealed class FoodLogEntry : Entity, ITenantOwned
{
    public const int MaximumNameLength = 255;

    private FoodLogEntry() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public MealType Slot { get; private set; }
    public FoodLogSource Source { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }

    /// <summary>The catalog food; null for a custom entry.</summary>
    public Guid? FoodItemId { get; private set; }
    public Guid? FoodUnitId { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal? BaseAmount { get; private set; }
    public string? UnitLabel { get; private set; }
    public string? UnitLabelAr { get; private set; }

    public decimal Calories { get; private set; }
    public decimal Protein { get; private set; }
    public decimal Carbohydrates { get; private set; }
    public decimal Fat { get; private set; }
    public decimal Fiber { get; private set; }

    public Guid? PlannedMealId { get; private set; }
    public Guid? PlannedMealFoodItemId { get; private set; }
    public Guid? ReplacementOptionId { get; private set; }

    public DateTimeOffset LoggedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsCustom => FoodItemId is null;
    public FoodLogNutrients Nutrients => new(Calories, Protein, Carbohydrates, Fat, Fiber);

    /// <summary>
    /// A catalog food. With a <paramref name="planned"/> reference it is the planned food, or a swap when it
    /// names the alternative; otherwise it is an extra.
    /// </summary>
    public static FoodLogEntry ForFood(
        Guid coachId, Guid clientId, DateOnly date, MealType slot, string nameEn, string? nameAr,
        FoodLogPortion portion, FoodLogNutrients nutrients, PlannedFoodReference? planned, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(portion);
        var entry = Start(coachId, clientId, date, slot, nameEn, nameAr, now);
        entry.Source = planned is null ? FoodLogSource.Extra
            : planned.ReplacementOptionId is null ? FoodLogSource.Planned : FoodLogSource.Swap;
        if (planned is not null)
        {
            entry.PlannedMealId = RequiredId(planned.MealId, "plannedMealId");
            entry.PlannedMealFoodItemId = RequiredId(planned.MealFoodItemId, "plannedMealFoodItemId");
            entry.ReplacementOptionId = planned.ReplacementOptionId is { } option ? RequiredId(option, "replacementOptionId") : null;
        }
        entry.SetPortion(portion, nutrients, now);
        return entry;
    }

    /// <summary>A food that is not in the catalog (a restaurant meal, for example), with nutrients as entered.</summary>
    public static FoodLogEntry Custom(
        Guid coachId, Guid clientId, DateOnly date, MealType slot, string nameEn, FoodLogNutrients nutrients, DateTimeOffset now)
    {
        var entry = Start(coachId, clientId, date, slot, nameEn, null, now);
        entry.Source = FoodLogSource.Extra;
        entry.SetNutrients(nutrients);
        return entry;
    }

    /// <summary>A new quantity or unit of the same food, with the nutrients recalculated for it.</summary>
    public void ChangePortion(FoodLogPortion portion, FoodLogNutrients nutrients, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(portion);
        if (IsCustom) throw new InvalidOperationException("A custom entry has no catalog food.");
        if (portion.FoodItemId != FoodItemId) throw new ArgumentException("An entry keeps its food.", nameof(portion));
        SetPortion(portion, nutrients, now);
    }

    public void ChangeCustom(string nameEn, FoodLogNutrients nutrients, DateTimeOffset now)
    {
        if (!IsCustom) throw new InvalidOperationException("Only a custom entry has entered nutrients.");
        NameEn = Text(nameEn);
        SetNutrients(nutrients);
        UpdatedAt = now;
    }

    public void MoveTo(MealType slot, DateTimeOffset now)
    {
        Slot = ValidSlot(slot);
        UpdatedAt = now;
    }

    private static FoodLogEntry Start(
        Guid coachId, Guid clientId, DateOnly date, MealType slot, string nameEn, string? nameAr, DateTimeOffset now) => new()
    {
        CoachId = TenantKey.Required(coachId),
        ClientId = RequiredId(clientId, nameof(clientId)),
        Date = date,
        Slot = ValidSlot(slot),
        NameEn = Text(nameEn),
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : Text(nameAr),
        LoggedAt = now,
        UpdatedAt = now
    };

    private void SetPortion(FoodLogPortion portion, FoodLogNutrients nutrients, DateTimeOffset now)
    {
        if (portion.Quantity <= 0 || portion.Quantity > 10000m) throw new ArgumentException("Must be above 0 and at most 10000.", "quantity");
        if (portion.BaseAmount <= 0) throw new ArgumentException("Must be above 0.", "baseAmount");
        FoodItemId = RequiredId(portion.FoodItemId, "foodItemId");
        FoodUnitId = portion.FoodUnitId is { } unit ? RequiredId(unit, "foodUnitId") : null;
        Quantity = portion.Quantity;
        BaseAmount = portion.BaseAmount;
        ArgumentException.ThrowIfNullOrWhiteSpace(portion.UnitLabel, "unitLabel");
        UnitLabel = portion.UnitLabel.Trim();
        UnitLabelAr = string.IsNullOrWhiteSpace(portion.UnitLabelAr) ? null : portion.UnitLabelAr.Trim();
        if (UnitLabel.Length > 50 || UnitLabelAr?.Length > 50) throw new ArgumentException("Unit labels are at most 50 characters.", "unitLabel");
        SetNutrients(nutrients);
        UpdatedAt = now;
    }

    private void SetNutrients(FoodLogNutrients nutrients)
    {
        ArgumentNullException.ThrowIfNull(nutrients);
        nutrients.Validate();
        (Calories, Protein, Carbohydrates, Fat, Fiber) =
            (nutrients.Calories, nutrients.Protein, nutrients.Carbohydrates, nutrients.Fat, nutrients.Fiber);
    }

    private static MealType ValidSlot(MealType slot) =>
        Enum.IsDefined(slot) ? slot : throw new ArgumentException("Unknown meal slot.", nameof(slot));

    private static string Text(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, "name");
        var normalized = value.Trim();
        return normalized.Length <= MaximumNameLength ? normalized
            : throw new ArgumentException($"At most {MaximumNameLength} characters.", "name");
    }

    private static Guid RequiredId(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException("Identifier is required.", name) : value;
}
