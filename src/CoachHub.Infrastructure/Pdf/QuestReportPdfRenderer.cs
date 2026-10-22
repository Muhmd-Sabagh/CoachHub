using System.Globalization;
using System.Text.RegularExpressions;
using CoachHub.Application.Adherence;
using CoachHub.Application.Pdf;
using CoachHub.Application.Progress;
using CoachHub.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoachHub.Infrastructure.Pdf;

/// <summary>
/// The weekly and monthly client report (§44) as a PDF: the coach's brand name and colour, then body, nutrition,
/// training, lifestyle, adherence and check-ins for the period, in English or Arabic (right to left).
/// </summary>
public sealed partial class QuestReportPdfRenderer : IReportPdfRenderer
{
    private const string Ink = "#101828";
    private const string Muted = "#667085";
    private const string Border = "#E4E7EC";
    private const string Light = "#F9FAFB";

    public QuestReportPdfRenderer() => PdfFonts.Ensure();

    public byte[] Render(ClientReport report, PlanPdfLanguage language)
    {
        var brand = HexColor().IsMatch(report.BrandColor) ? report.BrandColor : "#465FFF";
        var p = report.Progress;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style.FontFamily(PdfFonts.Family).FontSize(9).FontColor(Ink));
            page.Header().Column(column =>
            {
                column.Item().Height(5).Background(brand);
                column.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(report.BrandName).FontSize(16).Bold().FontColor(brand).Dir(language);
                        left.Item().Text(T(language, report.Period == ReportPeriod.Week ? "Weekly report" : "Monthly report",
                            report.Period == ReportPeriod.Week ? "التقرير الأسبوعي" : "التقرير الشهري")).FontSize(9).FontColor(Muted).Dir(language);
                    });
                    row.RelativeItem(2).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text(report.ClientName).FontSize(16).Bold().Dir(language);
                        right.Item().AlignRight().Text($"{D(report.From)} – {D(report.To)}").FontSize(8).FontColor(Muted);
                    });
                });
                column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Border);
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(10);
                column.Item().Background(Light).Border(1).BorderColor(Border).CornerRadius(8).Padding(10).Row(row =>
                {
                    Tile(row, T(language, "Overall adherence", "الالتزام العام"), Percent(p.Adherence.OverallPercent), brand, language);
                    Tile(row, T(language, "Workouts", "التمارين"), $"{p.Training.Summary.CompletedSessions} / {p.Training.Summary.ScheduledSessions}", brand, language);
                    Tile(row, T(language, "Avg calories", "متوسط السعرات"), p.Nutrition.AverageConsumed is { } eaten ? N(eaten.Calories, 0) : "–", brand, language);
                    Tile(row, T(language, "Weight change", "تغير الوزن"), Signed(p.Body.Weight.Change, " kg"), brand, language);
                });

                Section(column, T(language, "Body", "الجسم"), brand, language, table =>
                {
                    Head(table, language, T(language, "Measure", "القياس"), T(language, "Start", "البداية"), T(language, "Current", "الحالي"), T(language, "Change", "التغير"));
                    Series(table, T(language, "Weight (kg)", "الوزن (كجم)"), p.Body.Weight, language);
                    Series(table, T(language, "Body fat (%)", "نسبة الدهون (%)"), p.Body.BodyFat, language);
                    foreach (var site in p.Body.Measurements.Where(x => x.Summary.Count > 0))
                        Series(table, $"{site.Site} (cm)", site.Summary, language);
                });

                Section(column, T(language, "Nutrition", "التغذية"), brand, language, table =>
                {
                    Head(table, language, T(language, "", ""), T(language, "Calories", "السعرات"), T(language, "Protein", "البروتين"), T(language, "Carbs / Fat", "كربوهيدرات / دهون"));
                    Macro(table, T(language, "Average eaten", "المتوسط المتناول"), p.Nutrition.AverageConsumed, language);
                    Macro(table, T(language, "Average target", "متوسط الهدف"), p.Nutrition.AverageTarget, language);
                    Row(table, language, T(language, "Logged days", "أيام التسجيل"), $"{p.Nutrition.LoggedDays} / {p.Nutrition.Days}",
                        T(language, "Adherence", "الالتزام"), Percent(p.Nutrition.AdherencePercent));
                });

                Section(column, T(language, "Training", "التدريب"), brand, language, table =>
                {
                    Head(table, language, T(language, "Workouts", "التمارين"), T(language, "Sets", "المجموعات"), T(language, "Reps", "التكرارات"), T(language, "Volume (kg)", "الحجم (كجم)"));
                    Row(table, language, $"{p.Training.Summary.CompletedSessions} / {p.Training.Summary.ScheduledSessions}",
                        N(p.Training.Summary.Sets), N(p.Training.Summary.Reps), N(p.Training.Summary.VolumeKg, 0));
                    foreach (var record in p.Training.RecentRecords.Where(x => x.AchievedOn >= report.From && x.AchievedOn <= report.To).Take(8))
                        Row(table, language, T(language, "New record", "رقم قياسي"), PdfLanguageText.Name(record.ExerciseNameEn, record.ExerciseNameAr, language),
                            record.Type.ToString(), N(record.Value));
                });

                Section(column, T(language, "Lifestyle", "نمط الحياة"), brand, language, table =>
                {
                    Head(table, language, "", T(language, "Average", "المتوسط"), T(language, "Days on target", "أيام تحقيق الهدف"), T(language, "Target", "الهدف"));
                    var l = p.Lifestyle;
                    Row(table, language, T(language, "Steps", "الخطوات"), l.AverageSteps?.ToString(CultureInfo.InvariantCulture) ?? "–",
                        $"{l.StepsGoalDays} / {l.StepsDays}", p.Targets.Steps?.ToString(CultureInfo.InvariantCulture) ?? "–");
                    Row(table, language, T(language, "Sleep (h)", "النوم (ساعة)"), l.AverageSleepHours is { } h ? N(h) : "–",
                        $"{l.SleepGoalNights} / {l.SleepNights}", p.Targets.SleepHours is { } sleep ? N(sleep) : "–");
                    Row(table, language, T(language, "Water (ml)", "الماء (مل)"), l.AverageWaterMl?.ToString(CultureInfo.InvariantCulture) ?? "–",
                        $"{l.WaterGoalDays} / {l.WaterDays}", p.Targets.WaterMl?.ToString(CultureInfo.InvariantCulture) ?? "–");
                });

                Section(column, T(language, "Adherence", "الالتزام"), brand, language, table =>
                {
                    Head(table, language, T(language, "Area", "المجال"), T(language, "Score", "النتيجة"), T(language, "Weight", "الوزن النسبي"), "");
                    foreach (var area in p.Adherence.Areas)
                        Row(table, language, AreaName(area.Area, language), Percent(area.Percent), $"{area.Weight}%", "");
                    Row(table, language, T(language, "Overall", "الإجمالي"), Percent(p.Adherence.OverallPercent), "", "");
                });

                Section(column, T(language, "Check-ins", "المتابعات"), brand, language, table =>
                {
                    Head(table, language, T(language, "Due", "الموعد"), T(language, "Sent", "أُرسلت"), T(language, "Status", "الحالة"), T(language, "Replies", "الردود"));
                    foreach (var checkIn in report.CheckIns)
                        Row(table, language, D(checkIn.DueOn), checkIn.SubmittedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                            (checkIn.IsLate ? T(language, " (late)", " (متأخرة)") : ""), checkIn.Status.ToString(), N(checkIn.Replies));
                    var adherence = p.CheckIn.Adherence;
                    Row(table, language, T(language, "Done", "المنجزة"), $"{adherence.Completed} / {adherence.Due}", Percent(adherence.Percent), "");
                    if (p.CheckIn.LatestFeedback is { } feedback)
                        Row(table, language, T(language, "Latest feedback", "آخر ملاحظة"), feedback.Text, "", "");
                });
            });

            page.Footer().PaddingTop(6).BorderTop(1).BorderColor(Border).Row(row =>
            {
                row.RelativeItem().Text($"{report.BrandName}  •  {T(language, "Generated", "أُنشئ")} {report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC")
                    .FontSize(7).FontColor(Muted);
                row.RelativeItem().AlignRight().DefaultTextStyle(style => style.FontSize(7).FontColor(Muted)).Text(text =>
                {
                    text.Span(T(language, "Page ", "صفحة ")); text.CurrentPageNumber();
                    text.Span(T(language, " of ", " من ")); text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Tile(RowDescriptor row, string label, string value, string brand, PlanPdfLanguage language) =>
        row.RelativeItem().AlignCenter().Column(column =>
        {
            column.Item().AlignCenter().Text(value).FontSize(13).Bold().FontColor(brand);
            column.Item().AlignCenter().Text(label).FontSize(7).FontColor(Muted).Dir(language);
        });

    private static void Section(ColumnDescriptor column, string title, string brand, PlanPdfLanguage language, Action<TableDescriptor> rows) =>
        column.Item().Column(section =>
        {
            section.Item().PaddingBottom(4).Text(title).FontSize(11).Bold().FontColor(brand).Dir(language);
            section.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                rows(table);
            });
        });

    private static void Head(TableDescriptor table, PlanPdfLanguage language, params string[] cells)
    {
        foreach (var cell in cells)
            table.Cell().Background(Light).BorderBottom(1).BorderColor(Border).Padding(4).Text(cell).FontSize(7.5f).Bold().FontColor(Muted).Dir(language);
    }

    private static void Row(TableDescriptor table, PlanPdfLanguage language, params string[] cells)
    {
        foreach (var cell in cells)
            table.Cell().BorderBottom(1).BorderColor(Border).Padding(4).Text(cell).Dir(language);
    }

    private static void Series(TableDescriptor table, string label, SeriesSummary summary, PlanPdfLanguage language) =>
        Row(table, language, label, summary.Start is { } start ? N(start.Value) : "–", summary.Current is { } current ? N(current.Value) : "–", Signed(summary.Change, ""));

    private static void Macro(TableDescriptor table, string label, Application.FoodTracking.MacroAmounts? macros, PlanPdfLanguage language) =>
        Row(table, language, label, macros is null ? "–" : N(macros.Calories, 0), macros is null ? "–" : N(macros.Protein, 0) + " g",
            macros is null ? "–" : $"{N(macros.Carbohydrates, 0)} / {N(macros.Fat, 0)} g");

    private static string AreaName(AdherenceArea area, PlanPdfLanguage language) => area switch
    {
        AdherenceArea.Nutrition => T(language, "Nutrition", "التغذية"),
        AdherenceArea.Workout => T(language, "Workout", "التمرين"),
        AdherenceArea.CheckIn => T(language, "Check-in", "المتابعة"),
        AdherenceArea.Steps => T(language, "Steps", "الخطوات"),
        AdherenceArea.Water => T(language, "Water", "الماء"),
        AdherenceArea.Sleep => T(language, "Sleep", "النوم"),
        _ => area.ToString()
    };

    private static string Percent(decimal? value) => value is { } v ? v.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "–";
    private static string N(decimal value, int decimals = 1) => Math.Round(value, decimals).ToString("0.##", CultureInfo.InvariantCulture);
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Signed(decimal? value, string unit) =>
        value is { } v ? (v > 0 ? "+" : "") + Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture) + unit : "–";
    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string T(PlanPdfLanguage language, string english, string arabic) => language == PlanPdfLanguage.Arabic ? arabic : english;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}

internal static class ReportTextExtensions
{
    public static TextBlockDescriptor Dir(this TextBlockDescriptor text, PlanPdfLanguage language) =>
        language == PlanPdfLanguage.Arabic ? text.DirectionFromRightToLeft() : text;
}
