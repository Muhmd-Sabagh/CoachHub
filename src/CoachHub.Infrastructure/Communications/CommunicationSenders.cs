using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using CoachHub.Application.Communications;
using CoachHub.Domain.Communications;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Communications;

public sealed class CommunicationOptions
{
    public const string SectionName = "Communications";
    public string FromEmail { get; init; } = string.Empty; public string SmtpHost { get; init; } = string.Empty; public int SmtpPort { get; init; } = 587;
    public bool SmtpUseSsl { get; init; } = true; public string SmtpUser { get; init; } = string.Empty; public string SmtpPassword { get; init; } = string.Empty;
    public string WhatsAppEndpoint { get; init; } = "https://graph.facebook.com/v23.0"; public string WhatsAppPhoneNumberId { get; init; } = string.Empty; public string WhatsAppAccessToken { get; init; } = string.Empty;
}
public sealed class EmailNotificationSender(IOptions<CommunicationOptions> options) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Email;
    /// <summary>Sends from the configured address under the brand's display name, as HTML with a plain-text part.</summary>
    public async Task SendAsync(OutgoingMessage outgoing, CancellationToken token)
    {
        var o = options.Value; if (string.IsNullOrWhiteSpace(o.SmtpHost) || string.IsNullOrWhiteSpace(o.FromEmail)) throw new InvalidOperationException("SMTP is not configured.");
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = o.SmtpUseSsl, Credentials = string.IsNullOrWhiteSpace(o.SmtpUser) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(o.SmtpUser, o.SmtpPassword) };
        var (html, text) = BrandedEmail.Render(outgoing);
        using var message = new MailMessage(new MailAddress(o.FromEmail, outgoing.Branding.SenderName), new MailAddress(outgoing.Recipient))
        {
            Subject = outgoing.Subject, Body = text, SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));
        token.ThrowIfCancellationRequested(); await client.SendMailAsync(message, token);
    }
}
public sealed class WhatsAppNotificationSender(HttpClient http, IOptions<CommunicationOptions> options) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;
    public async Task SendAsync(OutgoingMessage outgoing, CancellationToken token)
    {
        var (recipient, subject, body) = (outgoing.Recipient, $"{outgoing.Branding.BrandName}: {outgoing.Subject}", outgoing.Body);
        var o = options.Value; if (string.IsNullOrWhiteSpace(o.WhatsAppPhoneNumberId) || string.IsNullOrWhiteSpace(o.WhatsAppAccessToken)) throw new InvalidOperationException("WhatsApp Cloud API is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.WhatsAppEndpoint.TrimEnd('/')}/{o.WhatsAppPhoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.WhatsAppAccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { messaging_product = "whatsapp", to = recipient, type = "text", text = new { body = $"{subject}\n\n{body}" } }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, token); response.EnsureSuccessStatusCode();
    }
}
