using CoachHub.Application.DietPlanning;
using CoachHub.Application.WorkoutPlanning;

namespace CoachHub.Application.Pdf;

public enum PlanPdfLanguage { English, Arabic }
public sealed record PdfClientInfo(string Name, string ClientCode);
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

public sealed record WorkoutPdfColumns(
    bool Sets, bool Repetitions, bool Rest, bool Tempo, bool RpeRir, bool Notes, bool Video);

public static class PdfColumnVisibility
{
    public static bool DietNotes(IEnumerable<MealFoodItemResponse> rows) =>
        rows.Any(row => Meaningful(row.Notes));

    public static WorkoutPdfColumns Workout(IEnumerable<WorkoutExerciseResponse> rows)
    {
        var values = rows.ToArray();
        return new(
            values.Any(x => Meaningful(x.Sets)), values.Any(x => Meaningful(x.Repetitions)),
            values.Any(x => Meaningful(x.Rest)), values.Any(x => Meaningful(x.Tempo)),
            values.Any(x => Meaningful(x.RpeRir)), values.Any(x => Meaningful(x.Notes)),
            values.Any(x => Meaningful(x.YouTubeUrl)));
    }

    private static bool Meaningful(string? value) => !string.IsNullOrWhiteSpace(value);
}

public static class PdfLanguageText
{
    public static string Name(string english, string? arabic, PlanPdfLanguage language) =>
        language == PlanPdfLanguage.Arabic && !string.IsNullOrWhiteSpace(arabic) ? arabic.Trim() : english;
}
