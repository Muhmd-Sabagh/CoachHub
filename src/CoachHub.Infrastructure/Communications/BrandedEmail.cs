using System.Net;
using System.Text;
using CoachHub.Application.Communications;

namespace CoachHub.Infrastructure.Communications;

/// <summary>
/// Renders a message in its brand's email layout (§63 email branding): the brand name in its
/// primary color, the brand's header and footer, and the body. Every value is HTML-encoded, so
/// brand text and message bodies cannot inject markup.
/// </summary>
public static class BrandedEmail
{
    public static (string Html, string Text) Render(OutgoingMessage message)
    {
        var brand = message.Branding;
        var color = IsHexColor(brand.PrimaryColor) ? brand.PrimaryColor : "#465fff";

        var html = new StringBuilder()
            .Append("<!doctype html><html><body style=\"margin:0;padding:24px;background:#f9fafb;font-family:Arial,Helvetica,sans-serif;color:#101828\">")
            .Append("<table role=\"presentation\" width=\"100%\" style=\"max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;border-top:4px solid ")
            .Append(color).Append("\"><tr><td style=\"padding:24px\">")
            .Append("<p style=\"margin:0 0 16px;font-size:20px;font-weight:bold;color:").Append(color).Append("\">")
            .Append(Encode(brand.BrandName)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(brand.EmailHeader))
            html.Append("<p style=\"margin:0 0 16px;color:#475467\">").Append(Paragraphs(brand.EmailHeader)).Append("</p>");
        html.Append("<h1 style=\"margin:0 0 12px;font-size:18px\">").Append(Encode(message.Subject)).Append("</h1>")
            .Append("<p style=\"margin:0 0 16px;line-height:1.5\">").Append(Paragraphs(message.Body)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(brand.EmailFooter))
            html.Append("<p style=\"margin:24px 0 0;font-size:12px;color:#667085\">").Append(Paragraphs(brand.EmailFooter)).Append("</p>");
        html.Append("</td></tr></table></body></html>");

        var text = new StringBuilder().AppendLine(brand.BrandName).AppendLine();
        if (!string.IsNullOrWhiteSpace(brand.EmailHeader)) text.AppendLine(brand.EmailHeader).AppendLine();
        text.AppendLine(message.Body);
        if (!string.IsNullOrWhiteSpace(brand.EmailFooter)) text.AppendLine().AppendLine("--").AppendLine(brand.EmailFooter);
        return (html.ToString(), text.ToString());
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Paragraphs(string value) =>
        Encode(value.Replace("\r\n", "\n", StringComparison.Ordinal)).Replace("\n", "<br>", StringComparison.Ordinal);

    private static bool IsHexColor(string value) =>
        value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}
