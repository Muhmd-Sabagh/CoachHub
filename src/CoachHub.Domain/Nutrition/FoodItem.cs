using System.Text.Json.Serialization;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Nutrition;

/// <summary>
/// What a food's nutrient values are measured per 100 of (§5, D3): grams, millilitres, or counted
/// pieces (a slice, a large egg, a scoop) for foods sold or eaten by the piece.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FoodBaseUnit>))]
public enum FoodBaseUnit
{
    Gram,
    Milliliter,
    Piece
}

/// <summary>
/// A food in the catalog (§5). Platform foods (no coach) are managed by administrators and visible to
/// every signed-in user; a coach's custom foods are visible to that coach and their clients only.
/// Nutrients are per 100 base units; named <see cref="Units"/> (a cup, a scoop) convert to base units.
/// </summary>
public sealed class FoodItem : Entity, ICatalogEntry
{
    public const decimal MaximumCaloriesPer100 = 15000m;
    public const decimal MaximumProteinPer100 = 5000m;
    public const decimal MaximumCarbohydratesPer100 = 5000m;
    public const decimal MaximumFatPer100 = 1000m;
    public const decimal MaximumFiberOrSugarPer100 = 5000m;
    public const decimal MaximumSodiumMgPer100 = 1_000_000m;
    public const decimal MaximumServingSize = 10000m;
    public const int MaximumUnits = 20;

    private readonly List<FoodUnit> _units = [];

    private FoodItem() { }

    /// <summary>The owning coach for a custom food; null for a platform food.</summary>
    public Guid? CoachId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid FoodCategoryId { get; private set; }
    public FoodBaseUnit BaseUnit { get; private set; }
    /// <summary>What one piece is called ("slice", "large egg"); only for <see cref="FoodBaseUnit.Piece"/>.</summary>
    public string? PieceNameEn { get; private set; }
    public string? PieceNameAr { get; private set; }
    /// <summary>A typical serving in base units (§5), used as the default quantity.</summary>
    public decimal ServingSize { get; private set; }
    public decimal CaloriesPer100 { get; private set; }
    public decimal ProteinPer100 { get; private set; }
    public decimal CarbohydratesPer100 { get; private set; }
    public decimal FatPer100 { get; private set; }
    /// <summary>Grams of fiber per 100 base units; null when not recorded.</summary>
    public decimal? FiberPer100 { get; private set; }
    /// <summary>Grams of sugar per 100 base units; null when not recorded.</summary>
    public decimal? SugarPer100 { get; private set; }
    /// <summary>Milligrams of sodium per 100 base units; null when not recorded.</summary>
    public decimal? SodiumMgPer100 { get; private set; }
    public Guid? MediaId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<FoodUnit> Units => _units;
    public bool IsPlatform => CoachId is null;

    /// <summary>The short label of the base unit: "g", "ml", or the piece name.</summary>
    public string BaseUnitLabel => BaseUnit switch
    {
        FoodBaseUnit.Gram => "g",
        FoodBaseUnit.Milliliter => "ml",
        _ => PieceNameEn ?? "piece"
    };

    /// <summary>The Arabic label of the base unit: "غ", "مل", or the piece name.</summary>
    public string BaseUnitLabelAr => BaseUnit switch
    {
        FoodBaseUnit.Gram => "غ",
        FoodBaseUnit.Milliliter => "مل",
        _ => PieceNameAr ?? PieceNameEn ?? "قطعة"
    };

    public static FoodItem Create(Guid? coachId, FoodValues values, IReadOnlyCollection<FoodUnitValues>? units = null)
    {
        var food = new FoodItem { CoachId = TenantKey.Optional(coachId) };
        food.Update(values, units ?? []);
        return food;
    }

    public void Update(FoodValues values, IReadOnlyCollection<FoodUnitValues> units)
    {
        ArgumentNullException.ThrowIfNull(values);
        NameEn = Required(values.NameEn, 255, "nameEn");
        NameAr = Optional(values.NameAr, 255, "nameAr");
        if (values.FoodCategoryId == Guid.Empty)
            throw new ArgumentException("A food category is required.", "foodCategoryId");
        FoodCategoryId = values.FoodCategoryId;
        if (!Enum.IsDefined(values.BaseUnit)) throw new ArgumentException("Unknown base unit.", "baseUnit");
        BaseUnit = values.BaseUnit;
        if (BaseUnit == FoodBaseUnit.Piece)
        {
            PieceNameEn = Required(values.PieceNameEn ?? string.Empty, 50, "pieceNameEn");
            PieceNameAr = Optional(values.PieceNameAr, 50, "pieceNameAr");
        }
        else
        {
            PieceNameEn = null;
            PieceNameAr = null;
        }
        if (values.ServingSize <= 0 || values.ServingSize > MaximumServingSize)
            throw new ArgumentOutOfRangeException("servingSize", $"Serving size must be above 0 and at most {MaximumServingSize}.");
        ServingSize = values.ServingSize;
        CaloriesPer100 = Nutrient(values.CaloriesPer100, MaximumCaloriesPer100, "caloriesPer100");
        ProteinPer100 = Nutrient(values.ProteinPer100, MaximumProteinPer100, "proteinPer100");
        CarbohydratesPer100 = Nutrient(values.CarbohydratesPer100, MaximumCarbohydratesPer100, "carbohydratesPer100");
        FatPer100 = Nutrient(values.FatPer100, MaximumFatPer100, "fatPer100");
        FiberPer100 = OptionalNutrient(values.FiberPer100, MaximumFiberOrSugarPer100, "fiberPer100");
        SugarPer100 = OptionalNutrient(values.SugarPer100, MaximumFiberOrSugarPer100, "sugarPer100");
        SodiumMgPer100 = OptionalNutrient(values.SodiumMgPer100, MaximumSodiumMgPer100, "sodiumMgPer100");
        MediaId = values.MediaId;
        IsActive = values.IsActive;
        SyncUnits(units);
    }

    /// <summary>
    /// The base-unit amount of <paramref name="quantity"/> in the given unit (the base unit when null).
    /// </summary>
    public decimal BaseAmount(decimal quantity, Guid? unitId)
    {
        if (unitId is not { } id) return quantity;
        var unit = _units.SingleOrDefault(x => x.Id == id)
            ?? throw new ArgumentException("The unit does not belong to this food.", nameof(unitId));
        return quantity * unit.BaseQuantity;
    }

    public FoodUnit? FindUnit(Guid? unitId) => unitId is { } id ? _units.SingleOrDefault(x => x.Id == id) : null;

    private void SyncUnits(IReadOnlyCollection<FoodUnitValues> units)
    {
        if (units.Count > MaximumUnits) throw new ArgumentException($"At most {MaximumUnits} units.", "units");
        var names = units.Select(x => x.NameEn.Trim().ToLowerInvariant()).ToArray();
        if (names.Distinct().Count() != names.Length) throw new ArgumentException("Unit names must be unique.", "units");
        var keep = units.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        if (keep.Any(id => _units.All(unit => unit.Id != id)))
            throw new ArgumentException("A unit to update does not belong to this food.", "units");
        _units.RemoveAll(unit => !keep.Contains(unit.Id));
        var order = 0;
        foreach (var values in units)
        {
            var existing = values.Id is { } id ? _units.Single(unit => unit.Id == id) : null;
            if (existing is null) _units.Add(FoodUnit.Create(Id, values, order));
            else existing.Update(values, order);
            order++;
        }
    }

    private static decimal Nutrient(decimal value, decimal maximum, string parameterName)
    {
        if (value < 0 || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between 0 and {maximum}.");
        return value;
    }

    private static decimal? OptionalNutrient(decimal? value, decimal maximum, string parameterName) =>
        value is { } amount ? Nutrient(amount, maximum, parameterName) : null;

    internal static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    internal static string? Optional(string? value, int maximumLength, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximumLength, parameterName);
}

/// <summary>
/// A named household or package unit of a food (§5, §8), worth <see cref="BaseQuantity"/> base units:
/// "1 cup" of oats = 80 g, "1 scoop" of whey = 30 g, "1 pack" of toast = 10 slices.
/// </summary>
public sealed class FoodUnit : Entity
{
    public const decimal MaximumBaseQuantity = 10000m;

    private FoodUnit() { }
    public Guid FoodItemId { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public int Order { get; private set; }

    internal static FoodUnit Create(Guid foodItemId, FoodUnitValues values, int order)
    {
        var unit = new FoodUnit { FoodItemId = foodItemId };
        unit.Update(values, order);
        return unit;
    }

    internal void Update(FoodUnitValues values, int order)
    {
        NameEn = FoodItem.Required(values.NameEn, 50, "units.nameEn");
        NameAr = FoodItem.Optional(values.NameAr, 50, "units.nameAr");
        if (values.BaseQuantity <= 0 || values.BaseQuantity > MaximumBaseQuantity)
            throw new ArgumentOutOfRangeException("units.baseQuantity", $"A unit must be worth more than 0 and at most {MaximumBaseQuantity} base units.");
        BaseQuantity = values.BaseQuantity;
        Order = order;
    }
}

/// <summary>Everything set on a food at once.</summary>
public sealed record FoodValues(
    string NameEn,
    string? NameAr,
    Guid FoodCategoryId,
    FoodBaseUnit BaseUnit,
    decimal ServingSize,
    decimal CaloriesPer100,
    decimal ProteinPer100,
    decimal CarbohydratesPer100,
    decimal FatPer100,
    decimal? FiberPer100 = null,
    decimal? SugarPer100 = null,
    decimal? SodiumMgPer100 = null,
    string? PieceNameEn = null,
    string? PieceNameAr = null,
    Guid? MediaId = null,
    bool IsActive = true);

/// <summary>A unit to keep (with its id) or add (without one).</summary>
public sealed record FoodUnitValues(Guid? Id, string NameEn, string? NameAr, decimal BaseQuantity);

/// <summary>Maps the free-text units of the legacy catalog to typed base units (D3).</summary>
public static class LegacyFoodUnits
{
    private static readonly Dictionary<string, string> PieceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["شريحة"] = "slice",
        ["كبيره الحجم"] = "large",
        ["سكوب"] = "scoop",
        ["كوب"] = "cup",
        ["معلقه كبيره"] = "tablespoon",
        ["رشه"] = "spray",
        ["piece"] = "piece"
    };

    /// <summary>Legacy values were per 100 of the stated unit, so counted units become pieces.</summary>
    public static (FoodBaseUnit Unit, string? PieceNameEn, string? PieceNameAr) Map(string? legacyUnit)
    {
        var unit = legacyUnit?.Trim() ?? string.Empty;
        if (unit.Length == 0 || unit.Equals("gram", StringComparison.OrdinalIgnoreCase) ||
            unit.Equals("g", StringComparison.OrdinalIgnoreCase) || unit.Equals("grams", StringComparison.OrdinalIgnoreCase))
            return (FoodBaseUnit.Gram, null, null);
        if (unit is "مل" || unit.Equals("ml", StringComparison.OrdinalIgnoreCase))
            return (FoodBaseUnit.Milliliter, null, null);
        var english = PieceNames.TryGetValue(unit, out var name) ? name : "piece";
        var arabic = unit.Any(character => character is >= '؀' and <= 'ۿ') ? unit : null;
        return (FoodBaseUnit.Piece, english, arabic);
    }
}
