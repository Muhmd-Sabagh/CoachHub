using System.Text.RegularExpressions;
using CoachHub.Application.Common.Localization;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;

namespace CoachHub.Application.Tests.Common;

/// <summary>The message catalog (P19): complete in both languages, in step with the code, and matched correctly.</summary>
public sealed partial class MessagesTests
{
    [Fact]
    public void Every_entry_has_both_languages_and_Arabic_uses_no_value_English_lacks()
    {
        Assert.NotEmpty(Messages.Entries);
        foreach (var entry in Messages.Entries.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.English), entry.Code);
            Assert.False(string.IsNullOrWhiteSpace(entry.Arabic), entry.Code);
            var english = Indexes(entry.English);
            Assert.True(Indexes(entry.Arabic).IsSubsetOf(english), $"{entry.Code}: the Arabic text uses a value the English text lacks.");
        }
    }

    [Fact]
    public void Every_matched_message_still_exists_in_the_code()
    {
        // A reworded message would silently lose its translation; this fails instead.
        var source = SourceText();
        var rendered = RenderedCode().Matches(source).Select(match => match.Groups[1].Value).ToHashSet();
        var stale = Messages.Entries.Values
            .Where(entry => !Messages.RenderedOnly.Any(prefix => entry.Code.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(entry => !rendered.Contains(entry.Code))
            .Where(entry => !FrameworkMessages.Contains(entry.Code))
            .Where(entry => !Fragments(entry.English).All(source.Contains))
            .Select(entry => entry.Code)
            .ToArray();
        Assert.True(stale.Length == 0, "Catalog entries whose English text no longer appears in the code: " + string.Join(", ", stale));
    }

    [Fact]
    public void Every_code_the_code_renders_is_in_the_catalog()
    {
        var used = RenderedCode().Matches(SourceText()).Select(match => match.Groups[1].Value).Distinct().ToArray();
        Assert.NotEmpty(used);
        var computed = Enum.GetNames<ClientFeature>().Select(name => $"access.feature.{name}")
            .Concat(new[] { "Expired", "Suspended", "Pending", "Inactive" }.Select(name => $"access.status.{name}"))
            .Concat(new[] { "diet", "workout" }.SelectMany(noun => new[] { $"notice.newVersion.{noun}", $"notice.updated.{noun}" }))
            .Concat(new[] { "notice.checkInApproved", "notice.checkInChanges", "notice.checkInReply" }.SelectMany(code => new[] { code + ".title", code + ".body" }));
        var missing = used.Concat(computed).Where(code => !Messages.Entries.ContainsKey(code)).ToArray();
        Assert.True(missing.Length == 0, "Missing catalog entries: " + string.Join(", ", missing));
    }

    [Fact]
    public void Exact_and_templated_messages_are_localized_with_their_code()
    {
        var exact = Messages.Localize("Food cannot be logged for a future day.", UiCulture.Arabic);
        Assert.Equal("food.futureDay", exact!.Code);
        Assert.Equal("لا يمكن تسجيل الطعام ليوم في المستقبل.", exact.Text);

        var templated = Messages.Localize("A workout has at most 20 exercises.", UiCulture.Arabic);
        Assert.Equal(("training.maxExercises", "لا يزيد التمرين على 20 تمرينًا."), (templated!.Code, templated.Text));

        var english = Messages.Localize("A workout has at most 20 exercises.", UiCulture.English);
        Assert.Equal("A workout has at most 20 exercises.", english!.Text);

        Assert.Null(Messages.Localize("A message nobody wrote.", UiCulture.Arabic));
        // Texts the code renders itself never match thrown messages.
        Assert.Null(Messages.Localize("The test is not available.", UiCulture.Arabic));
    }

    [Fact]
    public void Arabic_dates_use_the_Gregorian_calendar_and_Western_digits()
    {
        var text = Messages.Text("notice.subscriptionEnded.body", UiCulture.Arabic, new DateOnly(2026, 10, 23));
        Assert.Contains("23 أكتوبر 2026", text);
        Assert.Equal("Your subscription ended on 23 October 2026. Contact your coach to renew.",
            Messages.Text("notice.subscriptionEnded.body", UiCulture.English, new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void Every_subscription_status_reason_has_a_phrase()
    {
        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            var reason = status is SubscriptionStatus.Expired or SubscriptionStatus.Suspended or SubscriptionStatus.Pending
                ? status.ToString()
                : nameof(SubscriptionStatus.Inactive);
            Assert.True(Messages.Entries.ContainsKey($"access.status.{reason}"), reason);
        }
    }

    // ASP.NET (model validation) and ASP.NET Identity write these, not our code.
    private static readonly HashSet<string> FrameworkMessages =
    [
        "password.tooShort", "password.nonAlphanumeric", "password.digit", "password.lower", "password.upper", "password.mismatch",
        "password.invalidToken", "problem.fieldRequired", "problem.fieldInvalid", "problem.validationSummary"
    ];

    private static HashSet<int> Indexes(string template) =>
        Messages.Placeholder().Matches(template).Select(match => int.Parse(match.Value.Trim('{', '}').Split(':')[0])).ToHashSet();

    /// <summary>The literal pieces of a template, each of which must appear in the code.</summary>
    private static IEnumerable<string> Fragments(string template) =>
        Messages.Placeholder().Split(template).Where(part => part.Trim().Length >= 3);

    private static string SourceText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CoachHub.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return string.Join('\n', Directory.EnumerateFiles(Path.Combine(directory!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
    }

    [GeneratedRegex(@"(?:Messages\.Text|NoticeText\.Of)\(\s*""([a-zA-Z.]+)""")]
    private static partial Regex RenderedCode();
}
