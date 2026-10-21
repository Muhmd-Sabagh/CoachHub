using System.Text.Json.Serialization;
using CoachHub.Domain.FitnessTests;

namespace CoachHub.Application.FitnessTests;

/// <summary>
/// A test as entered (§34–§36). Cut-offs are optional and come as a pair. <paramref name="CoachId"/> is for
/// administrators creating a coach's custom test; without it an administrator creates a platform test.
/// </summary>
public sealed record TestDefinitionInput(
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
    bool IsActive = true,
    Guid? CoachId = null);

public sealed record TestDefinitionResponse(
    Guid Id,
    TestKind Kind,
    string NameEn,
    string? NameAr,
    TestUnit Unit,
    bool HigherIsBetter,
    decimal? GoodThreshold,
    decimal? PoorThreshold,
    string? InstructionsEn,
    string? InstructionsAr,
    string? YouTubeUrl,
    bool IsActive,
    Guid? CoachId,
    bool IsCustom);

/// <summary>Which tests a search covers: both libraries (default), the platform's, or the coach's own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestScope>))]
public enum TestScope { All, Platform, Custom }

public sealed record TestDefinitionQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public TestKind? Kind { get; init; }
    public bool? IsActive { get; init; }
    public TestScope Scope { get; init; } = TestScope.All;

    public TestDefinitionQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim()
    };
}

/// <summary>A new result, in the test's unit.</summary>
public sealed record TestResultInput(Guid TestDefinitionId, DateOnly Date, decimal Value, string? Notes = null);

public sealed record TestResultChangeInput(DateOnly Date, decimal Value, string? Notes = null);

/// <summary>The coach's classification of a result; null returns to the automatic one.</summary>
public sealed record TestClassificationInput(TestClassification? Classification);

public sealed record TestResultResponse(
    Guid Id,
    Guid TestDefinitionId,
    DateOnly Date,
    decimal Value,
    TestUnit Unit,
    TestClassification? Classification,
    bool IsClassificationOverridden,
    TestRecorder RecordedBy,
    string? Notes,
    DateTimeOffset CreatedAt);

/// <summary>A result's date and value.</summary>
public sealed record TestPoint(DateOnly Date, decimal Value);

/// <summary>How the current result compares with the previous one, in the test's direction.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TestTrend>))]
public enum TestTrend { Improved, Declined, Unchanged }

/// <summary>
/// Previous against current and the progress (§35, §36): changes are current − previous and current − first, in the
/// test's unit; the percentage is against the first result (none when it was zero).
/// </summary>
public sealed record TestProgressResponse(
    int Count,
    TestPoint? First,
    TestPoint? Previous,
    TestPoint? Current,
    TestPoint? Best,
    decimal? ChangeFromPrevious,
    decimal? ChangeFromFirst,
    decimal? PercentChangeFromFirst,
    TestTrend? Trend);

/// <summary>
/// One of the client's tests: assigned, or with results. <see cref="Definition"/> is null when the test is no longer
/// visible (a previous coach's custom test after a transfer); the name and unit then come from the latest result.
/// Results are newest first.
/// </summary>
public sealed record ClientTestResponse(
    Guid TestDefinitionId,
    TestKind Kind,
    string NameEn,
    string? NameAr,
    TestUnit Unit,
    bool HigherIsBetter,
    TestDefinitionResponse? Definition,
    bool IsAssigned,
    DateTimeOffset? AssignedAt,
    TestProgressResponse Progress,
    IReadOnlyList<TestResultResponse> Results);

public sealed record ClientTestsResponse(Guid ClientId, IReadOnlyList<ClientTestResponse> Tests);
