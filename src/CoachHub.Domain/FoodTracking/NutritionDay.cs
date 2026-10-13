using CoachHub.Domain.Common;

namespace CoachHub.Domain.FoodTracking;

/// <summary>
/// The client's choices for one day of their diet: which variant they follow ("Training day" or "Rest day")
/// and which option of each flexible meal (§13). A day without a row uses the first variant and the first
/// option of each meal. Choices point at plan version rows; when the plan moves to a new version they no
/// longer match and the defaults apply again.
/// </summary>
public sealed class NutritionDay : Entity, ITenantOwned
{
    private readonly List<NutritionDayMealChoice> _mealChoices = [];

    private NutritionDay() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid? VariantId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<NutritionDayMealChoice> MealChoices => _mealChoices;

    public static NutritionDay Create(Guid coachId, Guid clientId, DateOnly date, DateTimeOffset now) => new()
    {
        CoachId = TenantKey.Required(coachId),
        ClientId = clientId == Guid.Empty ? throw new ArgumentException("A client is required.", nameof(clientId)) : clientId,
        Date = date,
        UpdatedAt = now
    };

    public Guid? OptionFor(Guid mealId) => _mealChoices.SingleOrDefault(x => x.MealId == mealId)?.MealOptionId;

    public void ChooseVariant(Guid? variantId, DateTimeOffset now)
    {
        if (variantId == Guid.Empty) throw new ArgumentException("Variant identifier cannot be empty.", nameof(variantId));
        VariantId = variantId;
        UpdatedAt = now;
    }

    /// <summary>Chooses the option of a meal; null goes back to the meal's first option.</summary>
    public void ChooseOption(Guid mealId, Guid? optionId, DateTimeOffset now)
    {
        if (mealId == Guid.Empty || optionId == Guid.Empty) throw new ArgumentException("Identifiers cannot be empty.");
        var existing = _mealChoices.SingleOrDefault(x => x.MealId == mealId);
        if (optionId is not { } option)
        {
            if (existing is not null) _mealChoices.Remove(existing);
        }
        else if (existing is null)
        {
            if (_mealChoices.Count >= 50) throw new ArgumentException("Too many meal choices for one day.", nameof(mealId));
            _mealChoices.Add(NutritionDayMealChoice.Create(Id, mealId, option));
        }
        else
        {
            existing.Change(option);
        }
        UpdatedAt = now;
    }
}

/// <summary>The option of one meal chosen for the day.</summary>
public sealed class NutritionDayMealChoice : Entity
{
    private NutritionDayMealChoice() { }
    public Guid NutritionDayId { get; private set; }
    public Guid MealId { get; private set; }
    public Guid MealOptionId { get; private set; }

    internal static NutritionDayMealChoice Create(Guid dayId, Guid mealId, Guid optionId) =>
        new() { NutritionDayId = dayId, MealId = mealId, MealOptionId = optionId };

    internal void Change(Guid optionId) => MealOptionId = optionId;
}
