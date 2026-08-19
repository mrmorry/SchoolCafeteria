using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.Infrastructure.Adapters;

/// <summary>
/// SMTP implementation of IEmailSender (targets Mailhog in local Docker Compose). Swap for an
/// Azure Communication Services / SendGrid adapter in production purely via DI — no other code
/// depends on SMTP specifics.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _configuration["Email:Smtp:Host"] ?? "localhost";
        var port = int.Parse(_configuration["Email:Smtp:Port"] ?? "1025");
        var fromEmail = _configuration["Email:FromAddress"] ?? "no-reply@schoolcafeteria.local";
        var fromName = _configuration["Email:FromName"] ?? "SchoolCafeteria";
        var useAuth = bool.TryParse(_configuration["Email:Smtp:UseAuth"], out var auth) && auth;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, port, SecureSocketOptions.Auto, ct);
            if (useAuth)
                await client.AuthenticateAsync(_configuration["Email:Smtp:User"], _configuration["Email:Smtp:Password"], ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al enviar correo a {Recipient} (será reintentado por el worker de notificaciones).", toEmail);
            throw;
        }
    }
}
