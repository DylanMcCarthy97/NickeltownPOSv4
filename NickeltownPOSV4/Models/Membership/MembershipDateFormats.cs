using System;
using System.Globalization;

namespace NickeltownPOSV4.Models.Membership;

public static class MembershipDateFormats
{
    public static string FormatAustralianDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FormatAustralianDate(DateOnly? date) =>
        date.HasValue ? FormatAustralianDate(date.Value) : string.Empty;

    public static bool TryParseAustralianDate(string? value, out DateOnly date)
    {
        date = default;
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        string[] formats =
        [
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d/M/yy",
            "dd/MM/yy",
            "d MMM yyyy",
            "dd MMM yyyy",
            "yyyy-MM-dd",
        ];

        if (DateOnly.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }
}
