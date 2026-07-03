using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Settings;

/// <summary>SMTP settings used by all email actions (monthly report, stock report, ad-hoc).</summary>
public sealed class AppEmailConfig
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderPassword { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Nickeltown POS";

    /// <summary>Multi-recipient list; legacy single-recipient form is migrated into this list.</summary>
    public List<string> RecipientEmails { get; set; } = new();
}
