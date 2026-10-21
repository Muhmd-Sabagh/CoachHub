using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Progress;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Settings;

/// <summary>Platform settings (Administrator) and each coach's calculation defaults (§49).</summary>
public sealed class SettingsService(
    ISettingsRepository settings,
    BrandingService branding,
    ICoachRepository coaches,
    CoachOwnership ownership,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    /// <summary>What every signed-in screen needs: brand name, coach name, units and language.</summary>
    public async Task<WorkspaceSettingsResponse> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        var platform = await PlatformAsync(cancellationToken);
        var brand = await branding.EffectiveAsync(currentUser.CoachId, cancellationToken);
        var coach = currentUser.CoachId is { } coachId ? await coaches.FindAsync(coachId, cancellationToken) : null;
        return new(brand.BrandName, coach?.DisplayName, platform.Units, platform.DefaultLanguage);
    }

    public async Task<PlatformSettingsResponse> GetPlatformAsync(CancellationToken cancellationToken)
    {
        var stored = await settings.FindPlatformAsync(cancellationToken);
        return Map(stored ?? PlatformSettings.CreateDefault(timeProvider.GetUtcNow()), stored is not null);
    }

    /// <summary>The platform settings in force; defaults until an Administrator saves them.</summary>
    public async Task<PlatformSettings> PlatformAsync(CancellationToken cancellationToken) =>
        await settings.FindPlatformAsync(cancellationToken) ?? PlatformSettings.CreateDefault(timeProvider.GetUtcNow());

    public async Task<PlatformSettingsResponse> UpdatePlatformAsync(PlatformSettingsInput input, CancellationToken cancellationToken)
    {
        var stored = await settings.FindPlatformAsync(cancellationToken);
        var target = stored ?? PlatformSettings.CreateDefault(timeProvider.GetUtcNow());
        DomainValidation.Run(() => target.Update(
            input.DefaultLanguage, input.Units, input.EmailNotificationsEnabled, input.WhatsAppNotificationsEnabled,
            input.EmailSenderName, timeProvider.GetUtcNow()), "settings");
        if (stored is null) settings.Add(target);
        await settings.SaveChangesAsync(cancellationToken);
        return Map(target, true);
    }

    /// <summary>A coach reads their own defaults; an Administrator names the coach.</summary>
    public async Task<CoachSettingsResponse> GetCoachAsync(Guid? coachId, CancellationToken cancellationToken)
    {
        var owner = await ownership.ForNewRecordAsync(coachId, cancellationToken);
        var stored = await settings.FindCoachAsync(owner, cancellationToken);
        return Map(stored ?? CoachSettings.CreateDefault(owner, timeProvider.GetUtcNow()), stored is not null);
    }

    /// <summary>The defaults in force for a coach, for the calculation engines (P7, P8, P12).</summary>
    public async Task<CoachSettings> CoachAsync(Guid coachId, CancellationToken cancellationToken) =>
        await settings.FindCoachAsync(coachId, cancellationToken) ?? CoachSettings.CreateDefault(coachId, timeProvider.GetUtcNow());

    public async Task<CoachSettingsResponse> UpdateCoachAsync(Guid? coachId, CoachSettingsInput input, CancellationToken cancellationToken)
    {
        var owner = await ownership.ForNewRecordAsync(coachId, cancellationToken);
        var stored = await settings.FindCoachAsync(owner, cancellationToken);
        var target = stored ?? CoachSettings.CreateDefault(owner, timeProvider.GetUtcNow());
        if (input.ActivityFactors is null || input.AdherenceWeights is null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["settings"] = ["Activity factors and adherence weights are required."]
            });
        DomainValidation.Run(() => target.Update(
            input.ActivityFactors, input.DeficitPercent, input.SurplusPercent, input.ProteinGramsPerKg,
            input.FatGramsPerKg, input.DefaultMacroMode, input.AdherenceWeights, input.ReplacementRule,
            timeProvider.GetUtcNow()), "settings");
        DomainValidation.Run(() => target.UpdateTraining(
            input.OneRepMaxFormula, input.SecondaryMuscleWeight, timeProvider.GetUtcNow()), "settings");
        DomainValidation.Run(() => target.UpdateDefaultTargets(
            input.DefaultTargets ?? LifestyleTargetValues.None, timeProvider.GetUtcNow()), "defaultTargets");
        if (stored is null) settings.Add(target);
        await settings.SaveChangesAsync(cancellationToken);
        return Map(target, true);
    }

    private static PlatformSettingsResponse Map(PlatformSettings value, bool saved) => new(
        value.DefaultLanguage, value.Units, value.EmailNotificationsEnabled, value.WhatsAppNotificationsEnabled,
        value.EmailSenderName, saved ? value.UpdatedAt : null);

    private static CoachSettingsResponse Map(CoachSettings value, bool saved) => new(
        value.CoachId, saved, value.Factors, value.DeficitPercent, value.SurplusPercent, value.ProteinGramsPerKg,
        value.FatGramsPerKg, value.DefaultMacroMode, value.Weights, value.ReplacementRule, saved ? value.UpdatedAt : null,
        value.OneRepMaxFormula, value.SecondaryMuscleWeight, value.DefaultTargets);
}
