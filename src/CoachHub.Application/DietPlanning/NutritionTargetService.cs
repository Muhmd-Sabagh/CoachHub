using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Coaches;
using CoachHub.Application.Settings;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.DietPlanning;

/// <summary>
/// Calorie and macro targets (§14, §15) with the owning coach's coefficients (§49): ad hoc from the
/// calculator, or for a client from their profile and macro settings.
/// </summary>
public sealed class NutritionTargetService(
    SettingsService settings, IClientRepository clients, CoachOwnership ownership, TimeProvider timeProvider)
{
    public async Task<EnergyCalculatorResponse> CalculateAsync(EnergyCalculatorInput input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Age is < 10 or > 120) errors["age"] = ["Age must be between 10 and 120."];
        if (input.WeightKg is < 20 or > 400) errors["weightKg"] = ["Weight must be between 20 and 400 kg."];
        if (input.HeightCm is < 50 or > 260) errors["heightCm"] = ["Height must be between 50 and 260 cm."];
        if (!Enum.IsDefined(input.Sex)) errors["sex"] = ["Unknown sex."];
        if (!Enum.IsDefined(input.ActivityLevel)) errors["activityLevel"] = ["Unknown activity level."];
        if (!Enum.IsDefined(input.Goal)) errors["goal"] = ["Unknown goal."];
        if (errors.Count > 0) throw new ValidationException(errors);
        var macros = input.Macros is null ? null : DomainValidation.Run(() => input.Macros.Normalize(), "macros");

        var coach = await CoachSettingsAsync(input.CoachId, cancellationToken);
        return EnergyCalculator.Calculate(
            new EnergyProfile(input.Age, input.Sex, input.WeightKg, input.HeightCm, input.ActivityLevel, input.Goal),
            macros, coach);
    }

    /// <summary>
    /// The client's targets from their profile: age, sex, height, activity level and goal, with the latest weigh-in
    /// (P11; the profile's starting weight until there is one). Missing fields are reported per field.
    /// </summary>
    public async Task<EnergyCalculatorResponse> ForClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await clients.FindAsync(clientId, cancellationToken) ?? throw new NotFoundException("Client", clientId);
        var profile = await clients.FindProfileAsync(clientId, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var weightKg = await clients.FindLatestWeightKgAsync(clientId, cancellationToken) ?? profile?.StartingWeightKg;

        var missing = new Dictionary<string, string[]>();
        void Require(object? value, string field, string label)
        {
            if (value is null) missing[field] = [$"Add the client's {label} to their profile."];
        }
        Require(profile?.DateOfBirth, "dateOfBirth", "date of birth");
        Require(profile?.Sex, "sex", "sex");
        Require(profile?.HeightCm, "heightCm", "height");
        if (weightKg is null) missing["startingWeightKg"] = ["Log a weigh-in or add the client's starting weight to their profile."];
        Require(profile?.ActivityLevel, "activityLevel", "activity level");
        Require(profile?.Goal, "goal", "goal");
        if (missing.Count > 0) throw new ValidationException(missing);

        var coach = await settings.CoachAsync(client.CoachId, cancellationToken);
        return EnergyCalculator.Calculate(
            new EnergyProfile(profile!.AgeOn(today)!.Value,
                profile.Sex == Sex.Male ? BiologicalSex.Male : BiologicalSex.Female,
                weightKg!.Value, profile.HeightCm!.Value, profile.ActivityLevel!.Value, GoalFor(profile.Goal!.Value)),
            profile.Macros, coach);
    }

    /// <summary>Fat loss runs a deficit and muscle gain a surplus; every other goal eats at maintenance.</summary>
    public static EnergyGoal GoalFor(FitnessGoal goal) => goal switch
    {
        FitnessGoal.LoseFat => EnergyGoal.LoseWeight,
        FitnessGoal.BuildMuscle => EnergyGoal.GainWeight,
        _ => EnergyGoal.Maintain
    };

    /// <summary>A coach uses their own settings; an administrator names an existing coach whose settings apply.</summary>
    private async Task<CoachSettings> CoachSettingsAsync(Guid? requestedCoachId, CancellationToken cancellationToken) =>
        await settings.CoachAsync(await ownership.ForNewRecordAsync(requestedCoachId, cancellationToken), cancellationToken);
}
