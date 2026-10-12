using CoachHub.Application.Communications;
using CoachHub.Infrastructure.Communications;

namespace CoachHub.IntegrationTests.Communications;

/// <summary>Email branding (§63): the brand frames the message, and no text can inject markup.</summary>
public sealed class BrandedEmailTests
{
    [Fact]
    public void The_brand_name_color_header_and_footer_frame_the_message()
    {
        var (html, text) = BrandedEmail.Render(new OutgoingMessage("client@example.test", "Your plan", "Line one\nLine two",
            new MessageBranding("Coach A Fitness", "Coach A Fitness", "#aa0000", "Hello from Coach A", "Coach A, Cairo")));

        Assert.Contains("Coach A Fitness", html);
        Assert.Contains("#aa0000", html);
        Assert.Contains("Hello from Coach A", html);
        Assert.Contains("Coach A, Cairo", html);
        Assert.Contains("Line one<br>Line two", html);
        Assert.Contains("Coach A Fitness", text);
        Assert.Contains("Line two", text);
    }

    [Fact]
    public void Brand_text_and_bodies_are_html_encoded_and_bad_colors_fall_back()
    {
        var (html, _) = BrandedEmail.Render(new OutgoingMessage("client@example.test", "<b>Subject</b>", "<script>alert(1)</script>",
            new MessageBranding("<img src=x onerror=alert(1)>", "Brand", "red;background:url(x)", null, null)));

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("url(x)", html);
        Assert.Contains("#465fff", html);
    }
}
