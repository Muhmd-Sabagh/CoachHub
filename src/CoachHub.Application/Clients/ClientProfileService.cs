using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Nutrition;
using CoachHub.Domain.ReferenceData;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Clients;

/// <summary>A catalog item referenced by the profile, with its display names.</summary>
public sealed record NamedReference(Guid Id, string NameEn, string? NameAr);

public sealed record ClientProfileResponse(
    Guid ClientId,
    bool IsSaved,
    DateOnly? DateOfBirth,
    int? Age,
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
    IReadOnlyList<NamedReference> Equipment,
    string? FoodPreferences,
    IReadOnlyList<NamedReference> PreferredFoods,
    string? PreferredFoodsNote,
    IReadOnlyList<NamedReference> DislikedFoods,
    string? DislikedFoodsNote,
    IReadOnlyList<string> DietaryRestrictions,
    string? DietaryNotes,
    int? PreferredMealCount,
    TimeOnly? Bedtime,
    TimeOnly? WakeTime,
    string? LifestyleNotes,
    DateTimeOffset? UpdatedAt,
    MacroPreferences? Macros = null);

public sealed record ClientProfileInput(
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
    IReadOnlyList<Guid>? EquipmentIds,
    string? FoodPreferences,
    IReadOnlyList<Guid>? PreferredFoodIds,
    string? PreferredFoodsNote,
    IReadOnlyList<Guid>? DislikedFoodIds,
    string? DislikedFoodsNote,
    IReadOnlyList<string>? DietaryRestrictions,
    string? DietaryNotes,
    int? PreferredMealCount,
    TimeOnly? Bedtime,
    TimeOnly? WakeTime,
    string? LifestyleNotes,
    MacroPreferences? Macros = null);

/// <summary>The dietary restrictions a profile can carry, by name (the API exchanges names, not flags).</summary>
public static class DietaryRestrictionNames
{
    public static IReadOnlyList<string> All { get; } =
        Enum.GetValues<DietaryRestrictions>().Where(flag => flag != DietaryRestrictions.None).Select(flag => flag.ToString()).ToArray();

    public static IReadOnlyList<string> From(DietaryRestrictions flags) =>
        Enum.GetValues<DietaryRestrictions>().Where(flag => flag != DietaryRestrictions.None && flags.HasFlag(flag))
            .Select(flag => flag.ToString()).ToArray();

    public static DietaryRestrictions Parse(IReadOnlyList<string>? names)
    {
        var flags = DietaryRestrictions.None;
        foreach (var name in names ?? [])
        {
            if (!Enum.TryParse<DietaryRestrictions>(name, ignoreCase: true, out var flag) ||
                flag == DietaryRestrictions.None || !All.Contains(flag.ToString()))
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["dietaryRestrictions"] = [$"Unknown dietary restriction '{name}'."]
                });
            flags |= flag;
        }
        return flags;
    }
}

/// <summary>The structured client profile (§3, D3), entered at onboarding and editable by the coach.</summary>
public sealed class ClientProfileService(
    IClientRepository clients,
    IReferenceRepository<Equipment> equipment,
    IFoodRepository foods,
    TimeProvider timeProvider)
{
    public async Task<ClientProfileResponse> GetAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await RequireClientAsync(clientId, cancellationToken);
        var profile = await clients.FindProfileAsync(clientId, cancellationToken);
        return await MapAsync(clientId, profile, cancellationToken);
    }

    public async Task<ClientProfileResponse> UpdateAsync(Guid clientId, ClientProfileInput input, CancellationToken cancellationToken)
    {
        await RequireClientAsync(clientId, cancellationToken);
        var equipmentIds = input.EquipmentIds ?? [];
        var preferred = input.PreferredFoodIds ?? [];
        var disliked = input.DislikedFoodIds ?? [];
        await EnsureExistAsync(equipmentIds, id => equipment.FindAsync(id, cancellationToken), "equipmentIds", "Unknown equipment.");
        await EnsureExistAsync(preferred, id => foods.FindAsync(id, cancellationToken), "preferredFoodIds", "Unknown food.");
        await EnsureExistAsync(disliked, id => foods.FindAsync(id, cancellationToken), "dislikedFoodIds", "Unknown food.");

        var now = timeProvider.GetUtcNow();
        var existing = await clients.FindProfileAsync(clientId, cancellationToken);
        var profile = existing ?? ClientProfile.Create(clientId, now);
        DomainValidation.Run(() => profile.Update(new ClientProfileValues(
            input.DateOfBirth, input.Sex, input.HeightCm, input.StartingWeightKg, input.StartingBodyFatPercent,
            input.Goal, input.ActivityLevel, input.TrainingDaysPerWeek, input.AverageDailySteps, input.TrainingLocation,
            input.TrainingPreferences, input.FoodPreferences, preferred, input.PreferredFoodsNote, disliked,
            input.DislikedFoodsNote, DietaryRestrictionNames.Parse(input.DietaryRestrictions), input.DietaryNotes, equipmentIds, input.PreferredMealCount,
            input.Bedtime, input.WakeTime, input.LifestyleNotes, input.Macros), DateOnly.FromDateTime(now.UtcDateTime), now), "profile");

        if (existing is null) await clients.AddProfileAsync(profile, cancellationToken);
        else await clients.SaveChangesAsync(cancellationToken);
        return await MapAsync(clientId, profile, cancellationToken);
    }

    private async Task RequireClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        // Tenant-filtered: another coach's client is simply not found.
        _ = await clients.FindAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
    }

    private static async Task EnsureExistAsync<T>(
        IReadOnlyCollection<Guid> ids, Func<Guid, Task<T?>> find, string field, string message) where T : class
    {
        if (ids.Count > 50) throw Validation(field, "Choose at most 50 items.");
        foreach (var id in ids.Distinct())
            if (await find(id) is null) throw Validation(field, message);
    }

    private async Task<ClientProfileResponse> MapAsync(Guid clientId, ClientProfile? profile, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (profile is null)
            return new(clientId, false, null, null, null, null, null, null, null, null, null, null, null, null, [], null, [], null,
                [], null, [], null, null, null, null, null, null);

        var equipmentNames = new List<NamedReference>();
        foreach (var item in profile.Equipment)
            if (await equipment.FindAsync(item.EquipmentId, cancellationToken) is { } found)
                equipmentNames.Add(new(found.Id, found.NameEn, found.NameAr));
        async Task<IReadOnlyList<NamedReference>> FoodsAsync(FoodPreferenceKind kind)
        {
            var list = new List<NamedReference>();
            foreach (var item in profile.Foods.Where(food => food.Kind == kind))
                if (await foods.FindAsync(item.FoodItemId, cancellationToken) is { } food)
                    list.Add(new(food.Id, food.NameEn, food.NameAr));
            return list;
        }

        return new(clientId, true, profile.DateOfBirth, profile.AgeOn(today), profile.Sex, profile.HeightCm,
            profile.StartingWeightKg, profile.StartingBodyFatPercent, profile.Goal, profile.ActivityLevel,
            profile.TrainingDaysPerWeek, profile.AverageDailySteps, profile.TrainingLocation, profile.TrainingPreferences,
            equipmentNames, profile.FoodPreferences, await FoodsAsync(FoodPreferenceKind.Preferred), profile.PreferredFoodsNote,
            await FoodsAsync(FoodPreferenceKind.Disliked), profile.DislikedFoodsNote, DietaryRestrictionNames.From(profile.DietaryRestrictions),
            profile.DietaryNotes, profile.PreferredMealCount, profile.Bedtime, profile.WakeTime, profile.LifestyleNotes,
            profile.UpdatedAt, profile.Macros);
    }

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
