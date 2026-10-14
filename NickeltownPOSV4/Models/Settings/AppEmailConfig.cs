using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Settings;

/// <summary>SMTP settings used by all email actions (monthly report, stock report, shopping list, ad-hoc).</summary>
public sealed class AppEmailConfig
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderPassword { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Nickeltown POS";

    /// <summary>Multi-recipient list for stock/monthly reports; legacy single-recipient form is migrated into this list.</summary>
    public List<string> RecipientEmails { get; set; } = new();

    /// <summary>
    /// Sole recipient for automated Saturday shopping-list emails.
    /// Never seeded from <see cref="RecipientEmails"/> — must be set explicitly.
    /// </summary>
    public string ShoppingListRecipientEmail { get; set; } = string.Empty;

    /// <summary>When true, Saturday maintenance also sends the shopping-list email.</summary>
    public bool ShoppingListSaturdayEmailEnabled { get; set; }

    /// <summary>
    /// When true (default), skip the Saturday shopping-list email if nothing needs buying.
    /// When false, send a short confirmation that the list is empty.
    /// </summary>
    public bool ShoppingListSkipWhenEmpty { get; set; } = true;
}
