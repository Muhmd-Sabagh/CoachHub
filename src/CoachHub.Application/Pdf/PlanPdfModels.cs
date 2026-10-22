using System.Text.Json.Serialization;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.WorkoutPlanning;

namespace CoachHub.Application.Pdf;

[JsonConverter(typeof(JsonStringEnumConverter<PlanPdfLanguage>))]
public enum PlanPdfLanguage { English, Arabic }
public sealed record PdfClientInfo(string Name);
public sealed record GeneratedPlanPdf(byte[] Content, string FileName);

public interface IPlanPdfRenderer
{
    byte[] RenderDiet(DietPlanResponse plan, PdfClientInfo? client, PlanPdfLanguage language);
    byte[] RenderWorkout(WorkoutPlanResponse plan, PdfClientInfo? client, PlanPdfLanguage language);
}

public interface IPlanPdfClientRepository
{
    Task<PdfClientInfo?> FindAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Which workout columns have anything to show; empty columns are left out of the PDF.</summary>
public sealed record WorkoutPdfColumns(
    bool Sets, bool Reps, bool Load, bool Effort, bool Tempo, bool Rest, bool Notes, bool Video);

public static class PdfColumnVisibility
{
    public static bool DietNotes(IEnumerable<MealFoodItemResponse> rows) =>
        rows.Any(row => Meaningful(row.Notes));

    public static WorkoutPdfColumns Workout(IEnumerable<WorkoutExerciseResponse> rows)
    {
        var values = rows.ToArray();
        var summaries = values.Select(x => PrescriptionSummary.Of(x.Sets)).ToArray();
        return new(
            summaries.Any(x => Meaningful(x.Sets)), summaries.Any(x => Meaningful(x.Reps)),
            summaries.Any(x => Meaningful(x.Load)), summaries.Any(x => Meaningful(x.Effort)),
            summaries.Any(x => Meaningful(x.Tempo)), summaries.Any(x => Meaningful(x.Rest)),
            values.Any(x => Meaningful(x.Notes)), values.Any(x => Meaningful(x.YouTubeUrl)));
    }

    private static bool Meaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public static class PdfLanguageText
{
    public static string Name(string english, string? arabic, PlanPdfLanguage language) =>
        language == PlanPdfLanguage.Arabic && !string.IsNullOrWhiteSpace(arabic) ? arabic.Trim() : english;
}
