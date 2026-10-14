using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Lightweight email validation for Settings UI and Saturday shopping-list routing.</summary>
public static class EmailAddressValidation
{
    private static readonly Regex LooseShape = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsValid(string? email)
    {
        var t = (email ?? string.Empty).Trim();
        if (t.Length < 5 || t.Length > 254 || !LooseShape.IsMatch(t))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(t);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Normalize(string? email) => (email ?? string.Empty).Trim();
}
