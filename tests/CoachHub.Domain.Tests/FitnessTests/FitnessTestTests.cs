using CoachHub.Domain.FitnessTests;

namespace CoachHub.Domain.Tests.FitnessTests;

public sealed class FitnessTestTests
{
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static TestDefinition PushUps(decimal? good = 30, decimal? poor = 15) =>
        TestDefinition.Create(Coach, new TestDefinitionValues(TestKind.Strength, " Push-ups ", null, TestUnit.Repetitions, true, good, poor));

    private static TestDefinition Run(decimal? good = 600, decimal? poor = 780) =>
        TestDefinition.Create(null, new TestDefinitionValues(TestKind.Endurance, "2.4 km run", "جري", TestUnit.Seconds, false, good, poor));

    [Theory]
    [InlineData(35, TestClassification.Good)]
    [InlineData(30, TestClassification.Good)]
    [InlineData(29.5, TestClassification.NeedsImprovement)]
    [InlineData(15, TestClassification.NeedsImprovement)]
    [InlineData(14, TestClassification.Poor)]
    public void Higher_is_better_classifies_from_the_good_cut_off_up(decimal value, TestClassification expected) =>
        Assert.Equal(expected, PushUps().Classify(value));

    [Theory]
    [InlineData(540, TestClassification.Good)]
    [InlineData(600, TestClassification.Good)]
    [InlineData(700, TestClassification.NeedsImprovement)]
    [InlineData(780, TestClassification.NeedsImprovement)]
    [InlineData(781, TestClassification.Poor)]
    public void Lower_is_better_classifies_from_the_good_cut_off_down(decimal value, TestClassification expected) =>
        Assert.Equal(expected, Run().Classify(value));

    [Fact]
    public void Without_cut_offs_there_is_no_automatic_class()
    {
        var test = PushUps(null, null);
        Assert.Null(test.Classify(40));
        Assert.Equal("Push-ups", test.NameEn);
        Assert.False(test.IsPlatform);
        Assert.True(Run().IsPlatform);
    }

    [Fact]
    public void Cut_offs_come_as_a_pair_on_the_right_sides()
    {
        Assert.Equal("poorThreshold", Assert.Throws<ArgumentException>(() => PushUps(30, null)).ParamName);
        Assert.Equal("goodThreshold", Assert.Throws<ArgumentException>(() => PushUps(null, 15)).ParamName);
        Assert.Equal("poorThreshold", Assert.Throws<ArgumentException>(() => PushUps(15, 30)).ParamName);
        Assert.Equal("poorThreshold", Assert.Throws<ArgumentException>(() => Run(780, 600)).ParamName);
        Assert.Equal("goodThreshold", Assert.Throws<ArgumentException>(() => PushUps(-1, -5)).ParamName);
        // Equal cut-offs leave no "needs improvement" band.
        Assert.Equal(TestClassification.Poor, PushUps(20, 20).Classify(19));
    }

    [Fact]
    public void A_test_needs_a_name_a_known_kind_and_unit_and_a_youtube_video()
    {
        Assert.Equal("nameEn", Assert.Throws<ArgumentException>(() => TestDefinition.Create(Coach,
            new TestDefinitionValues(TestKind.Mobility, " ", null, TestUnit.Centimeters, true))).ParamName);
        Assert.Equal("kind", Assert.Throws<ArgumentException>(() => TestDefinition.Create(Coach,
            new TestDefinitionValues((TestKind)9, "Reach", null, TestUnit.Centimeters, true))).ParamName);
        Assert.Equal("unit", Assert.Throws<ArgumentException>(() => TestDefinition.Create(Coach,
            new TestDefinitionValues(TestKind.Mobility, "Reach", null, (TestUnit)9, true))).ParamName);
        Assert.Equal("youTubeUrl", Assert.Throws<ArgumentException>(() => TestDefinition.Create(Coach,
            new TestDefinitionValues(TestKind.Mobility, "Reach", null, TestUnit.Centimeters, true, YouTubeUrl: "https://example.com/v"))).ParamName);
        var test = TestDefinition.Create(Coach, new TestDefinitionValues(TestKind.Mobility, "Reach", null, TestUnit.Centimeters, true,
            YouTubeUrl: " https://youtu.be/abc ", InstructionsEn: "  "));
        Assert.Equal(("https://youtu.be/abc", null), (test.YouTubeUrl, test.InstructionsEn));
    }

    [Fact]
    public void A_result_keeps_a_copy_of_its_test_and_follows_the_cut_offs()
    {
        var test = PushUps();
        var result = TestResult.Create(test, Coach, Client, Today, 32, " felt strong ", TestRecorder.Client, Now);
        Assert.Equal((test.Id, "Push-ups", TestUnit.Repetitions, true, TestClassification.Good, "felt strong", TestRecorder.Client),
            (result.TestDefinitionId, result.NameEn, result.Unit, result.HigherIsBetter, result.Classification, result.Notes, result.RecordedBy));

        // Later changes to the test leave the result as measured.
        test.Update(new TestDefinitionValues(TestKind.Strength, "Push-ups v2", null, TestUnit.Repetitions, true, 40, 20));
        result.Change(Today, 25, null, Now.AddHours(1));
        Assert.Equal(("Push-ups", TestClassification.NeedsImprovement), (result.NameEn, result.Classification));
    }

    [Fact]
    public void The_coach_override_stays_until_cleared()
    {
        var result = TestResult.Create(PushUps(), Coach, Client, Today, 10, null, TestRecorder.Coach, Now);
        Assert.Equal(TestClassification.Poor, result.Classification);
        result.OverrideClassification(TestClassification.NeedsImprovement, Now);
        result.Change(Today, 40, null, Now);
        Assert.Equal((TestClassification.NeedsImprovement, true), (result.Classification, result.IsClassificationOverridden));
        result.OverrideClassification(null, Now);
        Assert.Equal((TestClassification.Good, false), (result.Classification, result.IsClassificationOverridden));
        Assert.Equal("classification", Assert.Throws<ArgumentException>(() => result.OverrideClassification((TestClassification)7, Now)).ParamName);
    }

    [Fact]
    public void A_result_value_is_zero_or_more_with_two_decimals_at_most()
    {
        var test = PushUps(null, null);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => TestResult.Create(test, Coach, Client, Today, -1, null, TestRecorder.Client, Now)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => TestResult.Create(test, Coach, Client, Today, 1.005m, null, TestRecorder.Client, Now)).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentException>(() => TestResult.Create(test, Coach, Client, Today, 1_000_001, null, TestRecorder.Client, Now)).ParamName);
        Assert.Equal("notes", Assert.Throws<ArgumentException>(() =>
            TestResult.Create(test, Coach, Client, Today, 1, new string('x', TestResult.MaximumNotesLength + 1), TestRecorder.Client, Now)).ParamName);
        var result = TestResult.Create(test, Coach, Client, Today, 0, null, TestRecorder.Client, Now);
        Assert.Null(result.Classification);
        Assert.Throws<ArgumentException>(() => result.Change(Today, -2, null, Now));
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void An_assignment_needs_a_coach_client_and_test()
    {
        Assert.Throws<ArgumentException>(() => TestAssignment.Create(Guid.Empty, Client, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() => TestAssignment.Create(Coach, Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() => TestAssignment.Create(Coach, Client, Guid.Empty, Now));
    }
}
