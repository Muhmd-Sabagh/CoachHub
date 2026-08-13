using CoachHub.Domain.Training;

namespace CoachHub.Domain.Tests.Training;

public sealed class ExerciseTests
{
    [Fact]
    public void Create_allows_optional_arabic_media_and_youtube_values()
    {
        var exercise = Exercise.Create(
            "  Squat  ",
            null,
            Guid.NewGuid(),
            null,
            " https://youtu.be/example ");

        Assert.Equal("Squat", exercise.NameEn);
        Assert.Null(exercise.NameAr);
        Assert.Null(exercise.MediaId);
        Assert.Equal("https://youtu.be/example", exercise.YouTubeUrl);
    }

    [Theory]
    [InlineData("http://youtube.com/watch?v=x")]
    [InlineData("https://youtube.com.evil.example/watch?v=x")]
    [InlineData("https://example.com/video")]
    [InlineData("not-a-url")]
    public void Create_rejects_non_https_or_non_youtube_links(string url)
    {
        Assert.Throws<ArgumentException>(() => Exercise.Create(
            "Squat", null, Guid.NewGuid(), null, url));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=x")]
    [InlineData("https://m.youtube.com/watch?v=x")]
    [InlineData("https://youtu.be/x")]
    [InlineData("https://www.youtube-nocookie.com/embed/x")]
    public void Youtube_validation_accepts_recognized_https_hosts(string url)
    {
        Assert.True(Exercise.IsValidYouTubeUrl(url));
    }
}