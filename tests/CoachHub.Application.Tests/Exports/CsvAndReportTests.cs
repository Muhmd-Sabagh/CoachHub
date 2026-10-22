using System.Text;
using CoachHub.Application.Exports;
using CoachHub.Application.Reports;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Tests.Exports;

public sealed class CsvAndReportTests
{
    private static string Text(byte[] csv)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.Equal(preamble, csv[..preamble.Length]);
        return Encoding.UTF8.GetString(csv[preamble.Length..]);
    }

    [Fact]
    public void Csv_quotes_when_needed_and_uses_invariant_formats()
    {
        var csv = Text(Csv.Write(["Name", "Value", "Day", "Ok"], [
            ["Plain", 81.5m, new DateOnly(2026, 10, 22), true],
            ["Comma, here", "He said \"hi\"", null, false],
            ["Line\nbreak", -3m, new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.FromHours(2)), SubscriptionStatus.Expired],
        ]));
        Assert.Equal(
            "Name,Value,Day,Ok\r\n" +
            "Plain,81.5,2026-10-22,Yes\r\n" +
            "\"Comma, here\",\"He said \"\"hi\"\"\",,No\r\n" +
            "\"Line\nbreak\",-3,2026-10-22 07:30:00Z,Expired\r\n", csv);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "'=HYPERLINK(\"x\")")]
    [InlineData("+1 555", "'+1 555")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM", "'@SUM")]
    [InlineData("Normal", "Normal")]
    [InlineData("محمد", "محمد")]
    public void Text_that_a_spreadsheet_would_run_is_neutralised(string value, string expected) =>
        Assert.Equal(expected, Csv.Cell(value));

    [Fact]
    public void Every_row_has_a_value_per_column() =>
        Assert.Throws<ArgumentException>(() => Csv.Write(["A", "B"], [["only one"]]));

    [Theory]
    [InlineData("\"Tired\"", false, "Tired")]
    [InlineData("81.5", false, "81.5")]
    [InlineData("[\"Chest\",\"Back\"]", false, "Chest, Back")]
    [InlineData("true", false, "Yes")]
    [InlineData("null", false, "")]
    [InlineData("{\"x\":1}", false, "{\"x\":1}")]
    [InlineData("not json", false, "not json")]
    [InlineData("\"m\"", true, "File")]
    public void Answers_export_as_text(string json, bool isFile, string expected) =>
        Assert.Equal(expected, ExportService.AnswerText(json, isFile));

    [Fact]
    public void Weeks_run_monday_to_sunday_and_months_are_calendar_months()
    {
        // 2026-10-22 is a Thursday.
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), ReportPeriods.Range(ReportPeriod.Week, new DateOnly(2026, 10, 22)));
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), ReportPeriods.Range(ReportPeriod.Week, new DateOnly(2026, 10, 25)));
        Assert.Equal((new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1)), ReportPeriods.Range(ReportPeriod.Week, new DateOnly(2026, 10, 26)));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), ReportPeriods.Range(ReportPeriod.Month, new DateOnly(2026, 2, 14)));
        Assert.Equal((new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)), ReportPeriods.Range(ReportPeriod.Month, new DateOnly(2028, 2, 29)));
    }
}
