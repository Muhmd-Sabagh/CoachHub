using System.Text.Json.Serialization;
using CoachHub.Domain.Common;
using CoachHub.Domain.Training;

namespace CoachHub.Domain.FitnessTests;

/// <summary>The kind of test (§34 mobility, §35 strength, §36 endurance).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestKind>))]
public enum TestKind
{
    Mobility,
    Strength,
    Endurance
}

/// <summary>What a test measures its result in (D3: a typed value, never free text).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestUnit>))]
public enum TestUnit
{
    Kilograms,
    Repetitions,
    Seconds,
    Meters,
    Centimeters,
    Degrees,
    /// <summary>A scored test, for example a 0–3 movement screen.</summary>
    Points,
    BeatsPerMinute
}

/// <summary>How a result rates (§34): Good, Needs improvement or Poor.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestClassification>))]
public enum TestClassification
{
    Good,
    NeedsImprovement,
    Poor
}

/// <summary>Who recorded a result: the client (a self-test) or their coach.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestRecorder>))]
public enum TestRecorder
{
    Client,
    Coach
}

/// <summary>
/// Classification from a test's two cut-offs. Good from the good cut-off on, Poor beyond the poor cut-off, Needs
/// improvement between; "on" and "beyond" follow the test's direction. Without cut-offs there is no automatic class.
/// </summary>
public static class TestCutOffs
{
    public const decimal MaximumValue = 1_000_000m;

    public static TestClassification? Classify(decimal value, bool higherIsBetter, decimal? good, decimal? poor)
    {
        if (good is not { } goodFrom || poor is not { } poorBeyond) return null;
        if (higherIsBetter)
            return value >= goodFrom ? TestClassification.Good : value < poorBeyond ? TestClassification.Poor : TestClassification.NeedsImprovement;
        return value <= goodFrom ? TestClassification.Good : value > poorBeyond ? TestClassification.Poor : TestClassification.NeedsImprovement;
    }

    /// <summary>Both cut-offs or neither, each a valid value, and the good one on the better side of the poor one.</summary>
    public static void Validate(bool higherIsBetter, decimal? good, decimal? poor)
    {
        if (good.HasValue != poor.HasValue)
            throw new ArgumentException("Set both cut-offs or neither.", good.HasValue ? "poorThreshold" : "goodThreshold");
        if (good is not { } goodFrom || poor is not { } poorBeyond) return;
        Value(goodFrom, "goodThreshold");
        Value(poorBeyond, "poorThreshold");
        if (higherIsBetter ? poorBeyond > goodFrom : poorBeyond < goodFrom)
            throw new ArgumentException(higherIsBetter
                ? "When higher is better, the poor cut-off cannot be above the good one."
                : "When lower is better, the poor cut-off cannot be below the good one.", "poorThreshold");
    }

    /// <summary>A measured value: zero or more, at most two decimals.</summary>
    public static decimal Value(decimal value, string parameterName)
    {
        if (value < 0 || value > MaximumValue)
            throw new ArgumentException($"A value is between 0 and {MaximumValue:0}.", parameterName);
        if (decimal.Round(value, 2) != value)
            throw new ArgumentException("A value has at most two decimals.", parameterName);
        return value;
    }
}

/// <summary>Everything set on a test at once. Cut-offs are optional; without them the coach classifies by hand.</summary>
public sealed record TestDefinitionValues(
    TestKind Kind,
    string NameEn,
    string? NameAr,
    TestUnit Unit,
    bool HigherIsBetter,
    decimal? GoodThreshold = null,
    decimal? PoorThreshold = null,
    string? InstructionsEn = null,
    string? InstructionsAr = null,
    string? YouTubeUrl = null,
    bool IsActive = true);

/// <summary>
/// A mobility, strength or endurance test (§34–§36): name, instructions, video, unit, direction and cut-offs. A
/// catalog entry like exercises: platform tests (no coach) are managed by administrators; a coach keeps custom tests.
/// </summary>
public sealed class TestDefinition : Entity, ICatalogEntry
{
    public const int MaximumNameLength = 255;
    public const int MaximumTextLength = 4000;

    private TestDefinition() { }

    public Guid? CoachId { get; private set; }
    public TestKind Kind { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public TestUnit Unit { get; private set; }
    public bool HigherIsBetter { get; private set; }
    public decimal? GoodThreshold { get; private set; }
    public decimal? PoorThreshold { get; private set; }
    public string? InstructionsEn { get; private set; }
    public string? InstructionsAr { get; private set; }
    public string? YouTubeUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsPlatform => CoachId is null;

    public static TestDefinition Create(Guid? coachId, TestDefinitionValues values)
    {
        var test = new TestDefinition { CoachId = TenantKey.Optional(coachId) };
        test.Update(values);
        return test;
    }

    public void Update(TestDefinitionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!Enum.IsDefined(values.Kind)) throw new ArgumentException("Unknown test kind.", "kind");
        if (!Enum.IsDefined(values.Unit)) throw new ArgumentException("Unknown unit.", "unit");
        TestCutOffs.Validate(values.HigherIsBetter, values.GoodThreshold, values.PoorThreshold);
        if (!Exercise.IsValidYouTubeUrl(values.YouTubeUrl) || values.YouTubeUrl?.Trim().Length > 500)
            throw new ArgumentException("A valid HTTPS YouTube URL is required.", "youTubeUrl");
        Kind = values.Kind;
        NameEn = Required(values.NameEn, MaximumNameLength, "nameEn");
        NameAr = Optional(values.NameAr, MaximumNameLength, "nameAr");
        Unit = values.Unit;
        HigherIsBetter = values.HigherIsBetter;
        GoodThreshold = values.GoodThreshold;
        PoorThreshold = values.PoorThreshold;
        InstructionsEn = Optional(values.InstructionsEn, MaximumTextLength, "instructionsEn");
        InstructionsAr = Optional(values.InstructionsAr, MaximumTextLength, "instructionsAr");
        YouTubeUrl = string.IsNullOrWhiteSpace(values.YouTubeUrl) ? null : values.YouTubeUrl.Trim();
        IsActive = values.IsActive;
    }

    public TestClassification? Classify(decimal value) => TestCutOffs.Classify(value, HigherIsBetter, GoodThreshold, PoorThreshold);

    private static string Required(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"At most {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximumLength, parameterName);
}

/// <summary>A test the coach asked the client to do; the client records results for assigned tests only.</summary>
public sealed class TestAssignment : Entity, ITenantOwned
{
    private TestAssignment() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid TestDefinitionId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public static TestAssignment Create(Guid coachId, Guid clientId, Guid testDefinitionId, DateTimeOffset now)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (testDefinitionId == Guid.Empty) throw new ArgumentException("A test is required.", nameof(testDefinitionId));
        return new TestAssignment
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = clientId,
            TestDefinitionId = testDefinitionId,
            AssignedAt = now
        };
    }
}

/// <summary>
/// One result of a test (§34–§36). It keeps a copy of the test's name, unit, direction and cut-offs when recorded,
/// so the history stays readable and comparable after the test changes or the client moves to another coach (D7).
/// The classification follows the cut-offs unless the coach overrides it.
/// </summary>
public sealed class TestResult : Entity, ITenantOwned
{
    public const int MaximumNotesLength = 1000;

    private TestResult() { }

    public Guid CoachId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid TestDefinitionId { get; private set; }
    public TestKind Kind { get; private set; }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public TestUnit Unit { get; private set; }
    public bool HigherIsBetter { get; private set; }
    public decimal? GoodThreshold { get; private set; }
    public decimal? PoorThreshold { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Value { get; private set; }
    public TestClassification? Classification { get; private set; }
    public bool IsClassificationOverridden { get; private set; }
    public TestRecorder RecordedBy { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TestResult Create(
        TestDefinition test, Guid coachId, Guid clientId, DateOnly date, decimal value, string? notes,
        TestRecorder recordedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(test);
        if (clientId == Guid.Empty) throw new ArgumentException("A client is required.", nameof(clientId));
        if (!Enum.IsDefined(recordedBy)) throw new ArgumentException("Unknown recorder.", nameof(recordedBy));
        var result = new TestResult
        {
            CoachId = TenantKey.Required(coachId),
            ClientId = clientId,
            TestDefinitionId = test.Id,
            Kind = test.Kind,
            NameEn = test.NameEn,
            NameAr = test.NameAr,
            Unit = test.Unit,
            HigherIsBetter = test.HigherIsBetter,
            GoodThreshold = test.GoodThreshold,
            PoorThreshold = test.PoorThreshold,
            RecordedBy = recordedBy,
            CreatedAt = now
        };
        result.Change(date, value, notes, now);
        return result;
    }

    /// <summary>Changes the date, value and notes; the automatic classification follows the value.</summary>
    public void Change(DateOnly date, decimal value, string? notes, DateTimeOffset now)
    {
        TestCutOffs.Value(value, "value");
        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > MaximumNotesLength })
            throw new ArgumentException($"Notes have at most {MaximumNotesLength} characters.", "notes");
        Value = value;
        Date = date;
        Notes = trimmed;
        if (!IsClassificationOverridden) Classification = Automatic;
        UpdatedAt = now;
    }

    /// <summary>The coach's classification; null returns to the automatic one from the cut-offs.</summary>
    public void OverrideClassification(TestClassification? classification, DateTimeOffset now)
    {
        if (classification is { } value && !Enum.IsDefined(value))
            throw new ArgumentException("Unknown classification.", "classification");
        IsClassificationOverridden = classification is not null;
        Classification = classification ?? Automatic;
        UpdatedAt = now;
    }

    private TestClassification? Automatic => TestCutOffs.Classify(Value, HigherIsBetter, GoodThreshold, PoorThreshold);
}
