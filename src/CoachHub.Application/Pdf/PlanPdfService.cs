using System.Text;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.WorkoutPlanning;

namespace CoachHub.Application.Pdf;

public sealed class PlanPdfService(
    DietPlanService dietPlans,
    WorkoutPlanService workoutPlans,
    IPlanPdfClientRepository clients,
    IPlanPdfRenderer renderer)
{
    public async Task<GeneratedPlanPdf> DietAsync(
        Guid id, PlanPdfLanguage language, CancellationToken cancellationToken)
    {
        var plan = await dietPlans.GetAsync(id, cancellationToken);
        var client = plan.ClientId.HasValue ? await clients.FindAsync(plan.ClientId.Value, cancellationToken) : null;
        return new(renderer.RenderDiet(plan, client, language),
            FileName(plan.NameEn, "diet-plan", language));
    }

    public async Task<GeneratedPlanPdf> WorkoutAsync(
        Guid id, PlanPdfLanguage language, CancellationToken cancellationToken)
    {
        var plan = await workoutPlans.GetAsync(id, cancellationToken);
        var client = plan.ClientId.HasValue ? await clients.FindAsync(plan.ClientId.Value, cancellationToken) : null;
        return new(renderer.RenderWorkout(plan, client, language),
            FileName(plan.NameEn, "workout-plan", language));
    }

    internal static string FileName(string planName, string kind, PlanPdfLanguage language)
    {
        var builder = new StringBuilder();
        foreach (var character in planName.Trim())
        {
            if (char.IsLetterOrDigit(character)) builder.Append(character);
            else if ((char.IsWhiteSpace(character) || character is '-' or '_') &&
                     builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        var safe = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(safe)) safe = "coachhub-plan";
        if (safe.Length > 80) safe = safe[..80].TrimEnd('-');
        return $"{safe}-{kind}-{(language == PlanPdfLanguage.Arabic ? "ar" : "en")}.pdf";
    }
}
