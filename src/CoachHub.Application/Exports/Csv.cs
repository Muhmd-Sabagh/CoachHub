using System.Globalization;
using System.Text;

namespace CoachHub.Application.Exports;

/// <summary>
/// CSV (RFC 4180) for the exports (§51), pure. UTF-8 with a byte-order mark so spreadsheet apps read Arabic text, CRLF
/// line ends, and fields quoted when needed. A text cell that begins with =, +, -, @, a tab or a carriage return is
/// prefixed with an apostrophe, so a spreadsheet never runs it as a formula (CSV injection).
/// </summary>
public static class Csv
{
    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        Line(builder, header);
        foreach (var row in rows)
        {
            if (row.Count != header.Count) throw new ArgumentException("Every row has a value per column.", nameof(rows));
            Line(builder, row);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    public static string Cell(object? value) => value switch
    {
        null => string.Empty,
        string text => Text(text),
        bool flag => flag ? "Yes" : "No",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Text(value.ToString() ?? string.Empty)
    };

    private static void Line(StringBuilder builder, IEnumerable<object?> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first) builder.Append(',');
            first = false;
            var cell = Cell(value);
            builder.Append(cell.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{cell.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : cell);
        }
        builder.Append("\r\n");
    }

    private static string Text(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
}
