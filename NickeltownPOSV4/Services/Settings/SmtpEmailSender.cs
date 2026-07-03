using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IEmailConfigService _config;

    public SmtpEmailSender(IEmailConfigService config) => _config = config;

    public async Task SendAsync(
        string subject,
        string body,
        IReadOnlyCollection<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _config.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(config.SmtpServer))
        {
            throw new InvalidOperationException("SMTP server is not configured.");
        }

        if (string.IsNullOrWhiteSpace(config.SenderEmail) || string.IsNullOrWhiteSpace(config.SenderPassword))
        {
            throw new InvalidOperationException("Sender email and password must be configured.");
        }

        var recipients = config.RecipientEmails
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("At least one recipient email is required.");
        }

        using var client = new SmtpClient(config.SmtpServer, config.SmtpPort)
        {
            EnableSsl = config.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(config.SenderEmail, config.SenderPassword),
            Timeout = 60_000,
        };

        using var message = new MailMessage
        {
            From = new MailAddress(config.SenderEmail, string.IsNullOrWhiteSpace(config.SenderName) ? config.SenderEmail : config.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };

        foreach (var to in recipients)
        {
            message.To.Add(to);
        }

        var streams = new List<Stream>();
        try
        {
            if (attachments is not null)
            {
                foreach (var att in attachments)
                {
                    var ms = new MemoryStream(att.Bytes, writable: false);
                    streams.Add(ms);
                    message.Attachments.Add(new Attachment(ms, att.FileName, att.MimeType));
                }
            }

            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var s in streams)
            {
                s.Dispose();
            }
        }
    }
}
