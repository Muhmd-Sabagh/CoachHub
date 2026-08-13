using System.Globalization;
using System.Reflection;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Pdf;
using CoachHub.Application.WorkoutPlanning;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoachHub.Infrastructure.Pdf;

public sealed class QuestPlanPdfRenderer : IPlanPdfRenderer
{
    private const string FontFamily = "Noto Sans Arabic";
    private const string Brand = "#465FFF";
    private const string BrandLight = "#ECF3FF";
    private const string Ink = "#101828";
    private const string Muted = "#667085";
    private const string Border = "#E4E7EC";
    private static readonly object FontLock = new();
    private static bool _initialized;

    public QuestPlanPdfRenderer()
    {
        lock (FontLock)
        {
            if (_initialized) return;
            QuestPDF.Settings.License = LicenseType.Community;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "CoachHub.Infrastructure.Pdf.Fonts.NotoSansArabic.ttf")
                ?? throw new InvalidOperationException("Embedded PDF font was not found.");
            FontManager.RegisterFont(stream);
            _initialized = true;
        }
    }

    public byte[] RenderDiet(DietPlanResponse plan, PdfClientInfo? client, PlanPdfLanguage language) =>
        Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, language);
            page.Header().Element(container => Header(container,
                PdfLanguageText.Name(plan.NameEn, plan.NameAr, language),
                T(language, "Diet plan", "خطة غذائية"), client, language));
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(12);
                column.Item().Element(container => DietSummary(container, plan, language));
                ActiveNotes(column, plan.Notes.Where(x => x.IsActive).Select(x => x.Text), language);
                var versions = plan.Versions.Where(x => x.IsActiveForPdf).OrderBy(x => x.Order).ToArray();
                if (versions.Length == 0)
                    column.Item().Element(container => Empty(container, T(language,
                        "No versions are enabled for PDF output.", "لا توجد نسخ مفعلة للعرض في ملف PDF."), language));
                foreach (var version in versions)
                    column.Item().Element(container => DietVersion(container, version, language));
            });
            page.Footer().Element(container => Footer(container, language));
        })).GeneratePdf();

    public byte[] RenderWorkout(WorkoutPlanResponse plan, PdfClientInfo? client, PlanPdfLanguage language) =>
        Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page, language);
            page.Header().Element(container => Header(container,
                PdfLanguageText.Name(plan.NameEn, plan.NameAr, language),
                T(language, "Workout plan", "خطة تدريبية"), client, language));
            page.Content().PaddingVertical(12).Column(column =>
            {
                column.Spacing(12);
                column.Item().Element(container => WorkoutSummary(container, plan, language));
                ActiveNotes(column, plan.Notes.Where(x => x.IsActive).Select(x => x.Text), language);
                foreach (var day in plan.Days.OrderBy(x => x.Order))
                    column.Item().Element(container => WorkoutDay(container, day, language));
            });
            page.Footer().Element(container => Footer(container, language));
        })).GeneratePdf();

    private static void ConfigurePage(PageDescriptor page, PlanPdfLanguage language)
    {
        page.Size(PageSizes.A4); page.Margin(28); page.PageColor(Colors.White);
        page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(9).FontColor(Ink));
    }

    private static void Header(
        IContainer container, string planName, string type, PdfClientInfo? client, PlanPdfLanguage language)
    {
        container.Column(column =>
        {
            column.Item().Height(5).Background(Brand);
            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("COACHHUB").FontSize(18).Bold().FontColor(Brand);
                    left.Item().Text(type).FontSize(9).FontColor(Muted);
                });
                row.RelativeItem(2).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(planName).FontSize(17).Bold().FontColor(Ink)
                        .DirectionAuto(language);
                    if (client is not null)
                        right.Item().AlignRight().Text($"{client.Name}  •  {client.ClientCode}")
                            .FontSize(8).FontColor(Muted).DirectionAuto(language);
                });
            });
            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Border);
        });
    }

    private static void DietSummary(IContainer container, DietPlanResponse plan, PlanPdfLanguage language)
    {
        container.Background(BrandLight).Border(1).BorderColor("#DDE9FF").CornerRadius(8).Padding(12).Row(row =>
        {
            Metric(row, T(language, "Weight", "الوزن"), Number(plan.Totals.Weight) + " g", language);
            Metric(row, T(language, "Calories", "السعرات"), Number(plan.Totals.Calories), language);
            Metric(row, T(language, "Protein", "البروتين"), Number(plan.Totals.Protein) + " g", language);
            Metric(row, T(language, "Carbs", "الكربوهيدرات"), Number(plan.Totals.Carbohydrates) + " g", language);
            Metric(row, T(language, "Fat", "الدهون"), Number(plan.Totals.Fat) + " g", language);
        });
    }

    private static void WorkoutSummary(IContainer container, WorkoutPlanResponse plan, PlanPdfLanguage language)
    {
        var exerciseCount = plan.Days.Sum(x => x.Exercises.Count);
        container.Background(BrandLight).Border(1).BorderColor("#DDE9FF").CornerRadius(8).Padding(12).Row(row =>
        {
            Metric(row, T(language, "Workout days", "أيام التدريب"), plan.Days.Count.ToString(CultureInfo.InvariantCulture), language);
            Metric(row, T(language, "Exercises", "التمارين"), exerciseCount.ToString(CultureInfo.InvariantCulture), language);
            Metric(row, T(language, "Created", "تاريخ الإنشاء"), plan.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), language);
        });
    }

    private static void Metric(RowDescriptor row, string label, string value, PlanPdfLanguage language)
    {
        row.RelativeItem().AlignCenter().Column(column =>
        {
            column.Item().AlignCenter().Text(value).FontSize(13).Bold().FontColor(Brand).DirectionAuto(language);
            column.Item().AlignCenter().Text(label).FontSize(7).FontColor(Muted).DirectionAuto(language);
        });
    }

    private static void ActiveNotes(ColumnDescriptor column, IEnumerable<string> notes, PlanPdfLanguage language)
    {
        var values = notes.ToArray();
        if (values.Length == 0) return;
        column.Item().Border(1).BorderColor(Border).CornerRadius(8).Padding(10).Column(noteColumn =>
        {
            noteColumn.Spacing(4);
            noteColumn.Item().Text(T(language, "Coach notes", "ملاحظات المدرب")).Bold().FontColor(Brand).DirectionAuto(language);
            foreach (var note in values)
                noteColumn.Item().Text("• " + note).FontColor(Ink).DirectionAuto(language);
        });
    }

    private static void DietVersion(IContainer container, DietPlanVersionResponse version, PlanPdfLanguage language)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Background(Brand).CornerRadius(6).Padding(9).Row(row =>
            {
                row.RelativeItem().Text(PdfLanguageText.Name(version.NameEn, version.NameAr, language))
                    .Bold().FontSize(11).FontColor(Colors.White).DirectionAuto(language);
                row.RelativeItem().AlignRight().Text($"{Number(version.Totals.Calories)} kcal")
                    .FontColor(Colors.White).DirectionAuto(language);
            });
            if (!string.IsNullOrWhiteSpace(version.Notes))
                column.Item().Text(version.Notes).FontColor(Muted).DirectionAuto(language);
            foreach (var meal in version.Meals.OrderBy(x => x.Order))
                column.Item().Element(mealContainer => DietMeal(mealContainer, meal,
                    version.ReplacementGroups.Where(x => x.TargetMealId == meal.Id).ToArray(), language));
        });
    }

    private static void DietMeal(
        IContainer container, MealResponse meal, IReadOnlyList<DietReplacementGroupResponse> replacements,
        PlanPdfLanguage language)
    {
        container.Border(1).BorderColor(Border).CornerRadius(7).Padding(9).Column(column =>
        {
            column.Spacing(6);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(PdfLanguageText.Name(meal.NameEn, meal.NameAr, language))
                    .Bold().FontSize(10).DirectionAuto(language);
                row.RelativeItem().AlignRight().Text($"{Number(meal.Totals.Calories)} kcal")
                    .FontColor(Brand).DirectionAuto(language);
            });
            if (!string.IsNullOrWhiteSpace(meal.Notes))
                column.Item().Text(meal.Notes).FontSize(8).FontColor(Muted).DirectionAuto(language);
            if (meal.FoodItems.Count > 0)
                column.Item().Table(table => DietFoodTable(table, meal.FoodItems, language));
            foreach (var group in replacements.OrderBy(x => x.Order))
                column.Item().Background("#F9FAFB").Padding(7).Column(alternatives =>
                {
                    alternatives.Item().Text(group.Title).Bold().FontSize(8).FontColor(Muted).DirectionAuto(language);
                    foreach (var option in group.Options.OrderBy(x => x.Order))
                    {
                        var label = option.ReplacementFoodItemId.HasValue
                            ? T(language, "Food alternative", "بديل غذائي")
                            : T(language, "Meal alternative", "وجبة بديلة");
                        var name = PdfLanguageText.Name(option.ReplacementNameEn, option.ReplacementNameAr, language);
                        alternatives.Item().Text($"• {label}: {name} - {Number(option.Totals.Calories)} kcal")
                            .FontSize(8).DirectionAuto(language);
                    }
                });
        });
    }

    private static void DietFoodTable(TableDescriptor table, IReadOnlyList<MealFoodItemResponse> rows, PlanPdfLanguage language)
    {
        var showNotes = PdfColumnVisibility.DietNotes(rows);
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(3); columns.RelativeColumn(); columns.RelativeColumn();
            columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
            if (showNotes) columns.RelativeColumn(2);
        });
        table.Header(header =>
        {
            HeaderCell(header, T(language, "Food", "الصنف"), language);
            HeaderCell(header, T(language, "Qty", "الكمية"), language);
            HeaderCell(header, T(language, "Calories", "السعرات"), language);
            HeaderCell(header, T(language, "Protein", "البروتين"), language);
            HeaderCell(header, T(language, "Carbs", "الكربوهيدرات"), language);
            HeaderCell(header, T(language, "Fat", "الدهون"), language);
            if (showNotes) HeaderCell(header, T(language, "Notes", "ملاحظات"), language);
        });
        foreach (var row in rows.OrderBy(x => x.Order))
        {
            BodyCell(table, PdfLanguageText.Name(row.FoodNameEn, row.FoodNameAr, language), language);
            BodyCell(table, $"{Number(row.Quantity)} {row.MeasurementUnit}", language);
            BodyCell(table, Number(row.Totals.Calories), language);
            BodyCell(table, Number(row.Totals.Protein), language);
            BodyCell(table, Number(row.Totals.Carbohydrates), language);
            BodyCell(table, Number(row.Totals.Fat), language);
            if (showNotes) BodyCell(table, row.Notes ?? string.Empty, language);
        }
    }

    private static void WorkoutDay(IContainer container, WorkoutDayResponse day, PlanPdfLanguage language)
    {
        container.Border(1).BorderColor(Border).CornerRadius(7).Padding(9).Column(column =>
        {
            column.Spacing(6);
            column.Item().Background(Brand).CornerRadius(5).Padding(8).Row(row =>
            {
                row.RelativeItem().Text(PdfLanguageText.Name(day.NameEn, day.NameAr, language))
                    .Bold().FontSize(11).FontColor(Colors.White).DirectionAuto(language);
                if (!string.IsNullOrWhiteSpace(day.Subtitle))
                    row.RelativeItem().AlignRight().Text(day.Subtitle).FontColor(Colors.White).DirectionAuto(language);
            });
            if (!string.IsNullOrWhiteSpace(day.Notes))
                column.Item().Text(day.Notes).FontSize(8).FontColor(Muted).DirectionAuto(language);
            if (day.Exercises.Count == 0)
                column.Item().Element(empty => Empty(empty, T(language, "No exercises prescribed.", "لا توجد تمارين محددة."), language));
            else column.Item().Table(table => WorkoutTable(table, day.Exercises, language));
        });
    }

    private static void WorkoutTable(TableDescriptor table, IReadOnlyList<WorkoutExerciseResponse> rows, PlanPdfLanguage language)
    {
        var visible = PdfColumnVisibility.Workout(rows);
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(3);
            if (visible.Sets) columns.RelativeColumn(); if (visible.Repetitions) columns.RelativeColumn();
            if (visible.Rest) columns.RelativeColumn(); if (visible.Tempo) columns.RelativeColumn();
            if (visible.RpeRir) columns.RelativeColumn(); if (visible.Notes) columns.RelativeColumn(2);
            if (visible.Video) columns.RelativeColumn();
        });
        table.Header(header =>
        {
            HeaderCell(header, T(language, "Exercise", "التمرين"), language);
            if (visible.Sets) HeaderCell(header, T(language, "Sets", "المجموعات"), language);
            if (visible.Repetitions) HeaderCell(header, T(language, "Reps", "التكرارات"), language);
            if (visible.Rest) HeaderCell(header, T(language, "Rest", "الراحة"), language);
            if (visible.Tempo) HeaderCell(header, T(language, "Tempo", "الإيقاع"), language);
            if (visible.RpeRir) HeaderCell(header, "RPE / RIR", language);
            if (visible.Notes) HeaderCell(header, T(language, "Notes", "ملاحظات"), language);
            if (visible.Video) HeaderCell(header, T(language, "Video", "فيديو"), language);
        });
        foreach (var row in rows.OrderBy(x => x.Order))
        {
            BodyCell(table, PdfLanguageText.Name(row.ExerciseNameEn, row.ExerciseNameAr, language), language);
            if (visible.Sets) BodyCell(table, row.Sets ?? string.Empty, language);
            if (visible.Repetitions) BodyCell(table, row.Repetitions ?? string.Empty, language);
            if (visible.Rest) BodyCell(table, row.Rest ?? string.Empty, language);
            if (visible.Tempo) BodyCell(table, row.Tempo ?? string.Empty, language);
            if (visible.RpeRir) BodyCell(table, row.RpeRir ?? string.Empty, language);
            if (visible.Notes) BodyCell(table, row.Notes ?? string.Empty, language);
            if (visible.Video)
            {
                var cell = table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(5).AlignCenter();
                if (!string.IsNullOrWhiteSpace(row.YouTubeUrl))
                    cell.Hyperlink(row.YouTubeUrl).Text(T(language, "Open", "فتح")).FontColor(Brand).Underline().DirectionAuto(language);
                else cell.Text(string.Empty);
            }
        }
    }

    private static void HeaderCell(TableCellDescriptor cells, string text, PlanPdfLanguage language) =>
        cells.Cell().Background("#F2F4F7").BorderBottom(1).BorderColor(Border).Padding(5)
            .Text(text).Bold().FontSize(7).FontColor("#344054").DirectionAuto(language);
    private static void BodyCell(TableDescriptor table, string text, PlanPdfLanguage language) =>
        table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(3)
            .Text(text).FontSize(7).DirectionAuto(language);

    private static void Empty(IContainer container, string text, PlanPdfLanguage language) =>
        container.Background("#F9FAFB").Padding(9).AlignCenter().Text(text).FontColor(Muted).DirectionAuto(language);

    private static void Footer(IContainer container, PlanPdfLanguage language)
    {
        container.PaddingTop(8).BorderTop(1).BorderColor(Border).Row(row =>
        {
            row.RelativeItem().Text("CoachHub").FontSize(7).FontColor(Muted);
            var pageNumber = row.RelativeItem().AlignRight()
                .DefaultTextStyle(style => style.FontSize(7).FontColor(Muted));
            pageNumber.Text(text =>
            {
                text.Span(T(language, "Page ", "صفحة ")); text.CurrentPageNumber();
                text.Span(T(language, " of ", " من ")); text.TotalPages();
            });
        });
    }

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string T(PlanPdfLanguage language, string english, string arabic) =>
        language == PlanPdfLanguage.Arabic ? arabic : english;
}

internal static class PdfDirectionExtensions
{
    public static TextBlockDescriptor DirectionAuto(
        this TextBlockDescriptor text, PlanPdfLanguage language) =>
        language == PlanPdfLanguage.Arabic ? text.DirectionFromRightToLeft() : text;
}
