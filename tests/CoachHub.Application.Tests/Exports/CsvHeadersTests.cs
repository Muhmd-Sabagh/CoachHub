using System.Globalization;
using System.Text.RegularExpressions;
using CoachHub.Application.Exports;

namespace CoachHub.Application.Tests.Exports;

/// <summary>P21: CSV column names follow the requester's language; values stay machine-readable.</summary>
public sealed partial class CsvHeadersTests
{
    /// <summary>The sport's own abbreviations, written the same in both languages (glossary).</summary>
    private static readonly HashSet<string> Shared = ["RIR", "RPE"];

    [Fact]
    public void Every_exported_column_has_an_Arabic_name()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "CoachHub.Application", "Exports", "ExportService.cs"));
        var headers = HeaderLists().Matches(source).SelectMany(match => Quoted().Matches(match.Groups[1].Value).Select(x => x.Groups[1].Value))
            .Concat(FieldRows().Matches(source).Select(match => match.Groups[1].Value))
            .Distinct().Where(x => !Shared.Contains(x)).ToArray();
        Assert.True(headers.Length > 40);

        using var arabic = new UiCultureScope("ar");
        var missing = headers.Where(header => CsvHeaders.Localize(header) == header).ToArray();
        Assert.True(missing.Length == 0, "Columns without an Arabic name: " + string.Join(", ", missing));
    }

    [Fact]
    public void English_keeps_the_names_and_Arabic_translates_them()
    {
        string[] english = ["Date", "Weight (kg)", "RIR"];
        using (new UiCultureScope("en")) Assert.Equal(english, CsvHeaders.Localize(english));
        using (new UiCultureScope("ar")) Assert.Equal(["التاريخ", "الوزن (كجم)", "RIR"], CsvHeaders.Localize(english));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CoachHub.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;
        public UiCultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    [GeneratedRegex(@"CsvHeaders\.Localize\(\[([^\]]*)\]")]
    private static partial Regex HeaderLists();

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\(""([^""]+)"", (?:client|profile)")]
    private static partial Regex FieldRows();
}
