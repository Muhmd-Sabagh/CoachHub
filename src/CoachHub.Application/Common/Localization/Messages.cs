using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoachHub.Application.Common.Localization;

/// <summary>The two interface languages (P19). The request's language comes from its <c>Accept-Language</c> header.</summary>
public static class UiCulture
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>The language of the current request: Arabic when the UI culture is Arabic, else English.</summary>
    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Arabic ? Arabic : English;

    /// <summary>
    /// The culture that formats dates and numbers inside a message. Arabic keeps the Gregorian calendar (the neutral
    /// "ar" culture defaults to the Hijri calendar) and .NET always writes Western digits (owner decision 2026-10-22).
    /// </summary>
    public static CultureInfo Formatting(string language) => language == Arabic ? ArabicFormatting : CultureInfo.InvariantCulture;

    private static readonly CultureInfo ArabicFormatting = CreateArabic();

    private static CultureInfo CreateArabic()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ar-EG").Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return CultureInfo.ReadOnly(culture);
    }
}

/// <summary>A message in one language with its stable code, which clients can translate themselves.</summary>
public sealed record LocalizedMessage(string Code, string Text);

/// <summary>
/// The catalog of messages people read (P19): validation and error messages, and notification texts. Each entry has a
/// stable code and an English and an Arabic template; <c>{0}</c>, <c>{1}</c> … mark values filled in at run time.
/// <para>
/// Error messages stay in English where they are thrown. <see cref="Localize"/> finds the entry whose English template
/// matches a message and returns it in the requested language, so a message not yet in the catalog stays English.
/// </para>
/// </summary>
public static partial class Messages
{
    private static readonly Lazy<Catalog> Data = new(Load);

    public static IReadOnlyDictionary<string, CatalogEntry> Entries => Data.Value.Entries;

    /// <summary>The message in <paramref name="language"/>, or null when the catalog doesn't have it.</summary>
    public static LocalizedMessage? Localize(string english, string language)
    {
        var catalog = Data.Value;
        if (catalog.Exact.TryGetValue(english, out var exact))
            return new LocalizedMessage(exact.Code, language == UiCulture.Arabic ? exact.Arabic : exact.English);
        foreach (var (pattern, entry) in catalog.Templates)
        {
            var match = pattern.Match(english);
            if (!match.Success) continue;
            var values = match.Groups.Cast<Group>().Skip(1).Select(group => (object?)group.Value).ToArray();
            var template = language == UiCulture.Arabic ? entry.Arabic : entry.English;
            return new LocalizedMessage(entry.Code, string.Format(CultureInfo.InvariantCulture, template, values));
        }
        return null;
    }

    /// <summary>The entry <paramref name="code"/> in <paramref name="language"/>, with its values formatted for it.</summary>
    public static string Text(string code, string language, params object?[] values)
    {
        if (!Data.Value.Entries.TryGetValue(code, out var entry))
            throw new KeyNotFoundException($"The message catalog has no entry '{code}'.");
        var template = language == UiCulture.Arabic ? entry.Arabic : entry.English;
        return string.Format(UiCulture.Formatting(language), template, values);
    }

    private static Catalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CoachHub.Application.Common.Localization.messages.json")
            ?? throw new InvalidOperationException("The message catalog is missing from the assembly.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new InvalidOperationException("The message catalog is empty.");
        var entries = raw.ToDictionary(x => x.Key, x => new CatalogEntry(x.Key, x.Value["en"], x.Value["ar"]));
        var exact = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        var templates = new List<(Regex, CatalogEntry)>();
        foreach (var entry in entries.Values)
        {
            // Texts the code renders itself (notifications, access refusals) are never matched against error messages:
            // their loose templates would catch unrelated ones.
            if (RenderedOnly.Any(prefix => entry.Code.StartsWith(prefix, StringComparison.Ordinal))) continue;
            if (Placeholder().IsMatch(entry.English)) templates.Add((Pattern(entry.English), entry));
            else exact.TryAdd(entry.English, entry);
        }
        // Longer templates first, so the most specific one wins.
        templates.Sort((a, b) => b.Item2.English.Length.CompareTo(a.Item2.English.Length));
        return new Catalog(entries, exact, templates);
    }

    /// <summary>Code prefixes of entries used only through <see cref="Text"/>.</summary>
    public static readonly string[] RenderedOnly = ["notice.", "access."];

    private static Regex Pattern(string template)
    {
        var parts = Placeholder().Split(template);
        return new Regex("^" + string.Join("(.+?)", parts.Select(Regex.Escape)) + "$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    /// <summary>A value placeholder in a template: <c>{0}</c>, or with a format, <c>{0:d MMMM yyyy}</c>.</summary>
    [GeneratedRegex(@"\{\d+(?::[^}]*)?\}")]
    public static partial Regex Placeholder();

    private sealed record Catalog(
        IReadOnlyDictionary<string, CatalogEntry> Entries,
        IReadOnlyDictionary<string, CatalogEntry> Exact,
        List<(Regex Pattern, CatalogEntry Entry)> Templates);
}

public sealed record CatalogEntry(string Code, string English, string Arabic);
