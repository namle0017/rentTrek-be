using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Helpers.Interfaces;
using RenTrek.BLL.Options;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Gửi email HTML qua SMTP (STARTTLS) theo <see cref="EmailSettings"/>.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender
{
    public async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Email SMTP chưa được cấu hình");

        var from = string.IsNullOrWhiteSpace(settings.FromAddress) ? settings.Username : settings.FromAddress;
        using var message = new MailMessage(new MailAddress(from, settings.FromName), new MailAddress(toAddress, toName))
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.Username, settings.Password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
