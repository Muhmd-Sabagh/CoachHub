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
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        var o = options.Value; if (string.IsNullOrWhiteSpace(o.SmtpHost) || string.IsNullOrWhiteSpace(o.FromEmail)) throw new InvalidOperationException("SMTP is not configured.");
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort) { EnableSsl = o.SmtpUseSsl, Credentials = string.IsNullOrWhiteSpace(o.SmtpUser) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(o.SmtpUser, o.SmtpPassword) };
        using var message = new MailMessage(o.FromEmail, recipient, subject, body); token.ThrowIfCancellationRequested(); await client.SendMailAsync(message, token);
    }
}
public sealed class WhatsAppNotificationSender(HttpClient http, IOptions<CommunicationOptions> options) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        var o = options.Value; if (string.IsNullOrWhiteSpace(o.WhatsAppPhoneNumberId) || string.IsNullOrWhiteSpace(o.WhatsAppAccessToken)) throw new InvalidOperationException("WhatsApp Cloud API is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{o.WhatsAppEndpoint.TrimEnd('/')}/{o.WhatsAppPhoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.WhatsAppAccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { messaging_product = "whatsapp", to = recipient, type = "text", text = new { body = $"{subject}\n\n{body}" } }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, token); response.EnsureSuccessStatusCode();
    }
}
