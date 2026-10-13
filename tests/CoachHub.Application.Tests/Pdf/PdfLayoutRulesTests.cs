using CoachHub.Application.DietPlanning;
using CoachHub.Application.Pdf;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Application.Tests.Pdf;

public sealed class PdfLayoutRulesTests
{
    [Fact]
    public void Optional_workout_columns_are_visible_only_when_any_row_has_content()
    {
        var rows = new[]
        {
            Exercise([Set(0, loadKg: 60)], notes: null),
            Exercise([Set(0)], notes: "Controlled")
        };

        var columns = PdfColumnVisibility.Workout(rows);
        Assert.True(columns.Sets);
        Assert.True(columns.Load);
        Assert.True(columns.Notes);
        Assert.False(columns.Reps);
        Assert.False(columns.Rest);
        Assert.False(columns.Tempo);
        Assert.False(columns.Effort);
        Assert.False(columns.Video);
    }

    [Fact]
    public void Arabic_name_falls_back_consistently_to_required_english_name()
    {
        Assert.Equal("خطة", PdfLanguageText.Name("Plan", " خطة ", PlanPdfLanguage.Arabic));
        Assert.Equal("Plan", PdfLanguageText.Name("Plan", null, PlanPdfLanguage.Arabic));
        Assert.Equal("Plan", PdfLanguageText.Name("Plan", "خطة", PlanPdfLanguage.English));
    }

    [Fact]
    public void Diet_note_column_uses_all_rows_not_individual_empty_cells()
    {
        var rows = new[]
        {
            new MealFoodItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Oats", null, null, "g", "غ", 100, 0, null, NutritionTotals.Zero),
            new MealFoodItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Eggs", null, null, "large", "كبيرة", 3, 1, "Boiled", NutritionTotals.Zero)
        };
        Assert.True(PdfColumnVisibility.DietNotes(rows));
    }

    private static WorkoutExerciseResponse Exercise(PrescribedSetResponse[] sets, string? notes) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Press", null, Guid.NewGuid(), null, null, true,
        0, notes, WorkoutBlock.Main, null, null, null, sets);

    private static PrescribedSetResponse Set(int order, decimal? loadKg = null) =>
        new(Guid.NewGuid(), order, SetType.Normal, null, null, loadKg, null, null, null, null, null, false, null);
}
