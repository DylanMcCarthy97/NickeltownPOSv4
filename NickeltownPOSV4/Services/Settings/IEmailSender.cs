using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface IEmailSender
{
    /// <summary>Sends using saved SMTP config to <c>AppEmailConfig.RecipientEmails</c> (stock/monthly list).</summary>
    Task SendAsync(
        string subject,
        string body,
        IReadOnlyCollection<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends using saved SMTP config to an explicit recipient list only (never merges stock recipients).</summary>
    Task SendToAsync(
        string subject,
        string body,
        IReadOnlyCollection<string> toRecipients,
        IReadOnlyCollection<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}

public sealed record EmailAttachment(string FileName, byte[] Bytes, string MimeType);
