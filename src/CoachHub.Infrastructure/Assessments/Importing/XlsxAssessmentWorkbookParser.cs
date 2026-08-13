using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Infrastructure.Assessments.Importing;

public sealed class XlsxAssessmentWorkbookParser : IAssessmentWorkbookParser
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;
    private const long MaximumExpandedBytes = 100 * 1024 * 1024;
    private const int MaximumRows = 10_000;
    private const int MaximumColumns = 500;
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public Task<ParsedWorksheet> ParseAsync(Stream stream, string sheetName, CancellationToken cancellationToken)
    {
        if (!stream.CanRead) throw Validation("file", "A readable .xlsx file is required.");
        if (stream.CanSeek && (stream.Length == 0 || stream.Length > MaximumFileSizeBytes))
            throw Validation("file", "The .xlsx file must be between 1 byte and 10 MB.");
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 5_000 || archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
                throw Validation("file", "The workbook expands beyond the safe processing limit.");
            var workbook = Load(RequiredEntry(archive, "xl/workbook.xml"));
            var relationships = Load(RequiredEntry(archive, "xl/_rels/workbook.xml.rels"));
            var sheet = workbook.Root?.Element(Spreadsheet + "sheets")?.Elements(Spreadsheet + "sheet")
                .SingleOrDefault(item => string.Equals((string?)item.Attribute("name"), sheetName, StringComparison.Ordinal));
            if (sheet is null) throw Validation("sheetName", "The configured worksheet was not found.");
            var relationId = (string?)sheet.Attribute(Relationships + "id")
                ?? throw Validation("file", "The worksheet relationship is missing.");
            var target = relationships.Root?.Elements(PackageRelationships + "Relationship")
                .SingleOrDefault(item => (string?)item.Attribute("Id") == relationId)?.Attribute("Target")?.Value
                ?? throw Validation("file", "The worksheet target is missing.");
            var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.Replace('\\', '/');
            while (sheetPath.Contains("../", StringComparison.Ordinal))
                sheetPath = sheetPath.Replace("xl/../", string.Empty, StringComparison.Ordinal);
            var sharedStrings = ReadSharedStrings(archive);
            var dateStyles = ReadDateStyles(archive);
            var date1904 = string.Equals(
                (string?)workbook.Root?.Element(Spreadsheet + "workbookPr")?.Attribute("date1904"), "1",
                StringComparison.Ordinal);
            var document = Load(RequiredEntry(archive, sheetPath));
            var rows = document.Descendants(Spreadsheet + "row").Take(MaximumRows + 2).ToArray();
            if (rows.Length == 0) throw Validation("file", "The worksheet has no rows.");
            if (rows.Length > MaximumRows + 1) throw Validation("file", "The worksheet exceeds 10,000 data rows.");
            var headerCells = ReadCells(rows[0], sharedStrings, dateStyles, date1904);
            var headersByColumn = headerCells.Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(item => item.Key, item => item.Value!.Trim());
            if (headersByColumn.Count == 0) throw Validation("file", "The worksheet header row is empty.");
            if (headersByColumn.Keys.Max() >= MaximumColumns) throw Validation("file", "The worksheet exceeds 500 columns.");
            var normalized = headersByColumn.Values.Select(AssessmentImportService.NormalizeHeader).ToArray();
            if (normalized.Distinct().Count() != normalized.Length)
                throw Validation("file", "The worksheet contains duplicate headers after normalization.");
            var parsedRows = new List<ParsedWorkbookRow>();
            for (var index = 1; index < rows.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cells = ReadCells(rows[index], sharedStrings, dateStyles, date1904)
                    .Where(item => headersByColumn.ContainsKey(item.Key))
                    .ToDictionary(item => AssessmentImportService.NormalizeHeader(headersByColumn[item.Key]), item => item.Value);
                if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
                var rowNumber = int.TryParse((string?)rows[index].Attribute("r"), out var declared) ? declared : index + 1;
                parsedRows.Add(new(rowNumber, cells));
            }
            return Task.FromResult(new ParsedWorksheet(sheetName, headersByColumn.OrderBy(item => item.Key).Select(item => item.Value).ToArray(), parsedRows));
        }
        catch (InvalidDataException exception)
        {
            throw Validation("file", $"The file is not a valid .xlsx workbook: {exception.Message}");
        }
        catch (XmlException exception)
        {
            throw Validation("file", $"The workbook XML is invalid: {exception.Message}");
        }
    }

    private static Dictionary<int, string?> ReadCells(
        XElement row, IReadOnlyList<string> sharedStrings, IReadOnlySet<int> dateStyles, bool date1904)
    {
        var result = new Dictionary<int, string?>();
        foreach (var cell in row.Elements(Spreadsheet + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            var column = ColumnIndex(reference);
            if (column < 0 || column >= MaximumColumns) continue;
            var type = (string?)cell.Attribute("t");
            var raw = cell.Element(Spreadsheet + "v")?.Value;
            string? value = type switch
            {
                "s" when int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count => sharedStrings[sharedIndex],
                "inlineStr" => string.Concat(cell.Element(Spreadsheet + "is")?.Descendants(Spreadsheet + "t").Select(item => item.Value) ?? []),
                "b" => raw == "1" ? "true" : "false",
                _ => raw
            };
            if (type is null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                int.TryParse((string?)cell.Attribute("s"), out var style) && dateStyles.Contains(style))
            {
                if (date1904) number += 1462;
                value = DateTime.FromOADate(number).ToString("O", CultureInfo.InvariantCulture);
            }
            if (value is { Length: > 20_000 }) throw Validation("file", "A workbook cell exceeds 20,000 characters.");
            result[column] = value;
        }
        return result;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        return Load(entry).Root?.Elements(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToArray() ?? [];
    }
    private static IReadOnlySet<int> ReadDateStyles(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/styles.xml");
        if (entry is null) return new HashSet<int>();
        var document = Load(entry);
        var formats = document.Root?.Element(Spreadsheet + "numFmts")?.Elements(Spreadsheet + "numFmt")
            .Where(item => IsDateFormat((string?)item.Attribute("formatCode")))
            .Select(item => (int?)item.Attribute("numFmtId"))
            .Where(item => item.HasValue).Select(item => item!.Value).ToHashSet() ?? [];
        var builtIn = new HashSet<int>(Enumerable.Range(14, 9).Concat(Enumerable.Range(27, 10)).Concat(Enumerable.Range(45, 3)).Concat(Enumerable.Range(50, 9)));
        return document.Root?.Element(Spreadsheet + "cellXfs")?.Elements(Spreadsheet + "xf")
            .Select((item, index) => new { index, numberFormat = (int?)item.Attribute("numFmtId") ?? 0 })
            .Where(item => builtIn.Contains(item.numberFormat) || formats.Contains(item.numberFormat))
            .Select(item => item.index).ToHashSet() ?? [];
    }
    private static bool IsDateFormat(string? format) =>
        !string.IsNullOrWhiteSpace(format) && format.Replace("\\", string.Empty, StringComparison.Ordinal)
            .IndexOfAny(['y', 'm', 'd', 'h', 's']) >= 0;
    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var result = 0;
        var length = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character)) break;
            result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            length++;
        }
        return length == 0 ? -1 : result - 1;
    }
    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path) =>
        archive.GetEntry(path) ?? throw Validation("file", $"Required workbook part '{path}' is missing.");
    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumExpandedBytes
        });
        return XDocument.Load(reader, LoadOptions.None);
    }
    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}