using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface IEmailSender
{
    /// <summary>Sends a message using the saved email config; throws on failure (caller handles UI).</summary>
    Task SendAsync(
        string subject,
        string body,
        IReadOnlyCollection<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}

public sealed record EmailAttachment(string FileName, byte[] Bytes, string MimeType);
