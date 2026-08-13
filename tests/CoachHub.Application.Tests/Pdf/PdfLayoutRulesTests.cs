using CoachHub.Application.DietPlanning;
using CoachHub.Application.Pdf;
using CoachHub.Application.WorkoutPlanning;

namespace CoachHub.Application.Tests.Pdf;

public sealed class PdfLayoutRulesTests
{
    [Fact]
    public void Optional_workout_columns_are_visible_only_when_any_row_has_content()
    {
        var rows = new[]
        {
            Exercise(sets: "3", notes: null),
            Exercise(sets: null, notes: "Controlled")
        };

        var columns = PdfColumnVisibility.Workout(rows);
        Assert.True(columns.Sets);
        Assert.True(columns.Notes);
        Assert.False(columns.Repetitions);
        Assert.False(columns.Rest);
        Assert.False(columns.Tempo);
        Assert.False(columns.RpeRir);
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
            new MealFoodItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Oats", null, "g", 100, 0, null, NutritionTotals.Zero),
            new MealFoodItemResponse(Guid.NewGuid(), Guid.NewGuid(), "Eggs", null, "g", 100, 1, "Boiled", NutritionTotals.Zero)
        };
        Assert.True(PdfColumnVisibility.DietNotes(rows));
    }

    private static WorkoutExerciseResponse Exercise(string? sets, string? notes) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Press", null, Guid.NewGuid(), null, null, true,
        0, sets, null, null, null, null, notes);
}
