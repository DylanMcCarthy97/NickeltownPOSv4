using System.Globalization;

namespace NickeltownPOSV4.Services.Stock;

internal static class StockMoneyInputParser
{
    public static bool TryParseMoney(string? text, out decimal value)
    {
        var s = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(s))
        {
            value = 0m;
            return false;
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses a non-negative whole number; tolerates values like "12.0" from the numpad overlay.</summary>
    public static bool TryParseWholeNonNegativeInt(string? text, string fieldLabel, out int value, out string errorMessage)
    {
        value = 0;
        errorMessage = string.Empty;
        var s = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s))
        {
            errorMessage = $"{fieldLabel} is required.";
            return false;
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            errorMessage = $"{fieldLabel} must be a valid number.";
            return false;
        }

        if (d < 0)
        {
            errorMessage = $"{fieldLabel} must be zero or greater.";
            return false;
        }

        if (d != decimal.Truncate(d))
        {
            errorMessage = $"{fieldLabel} must be a whole number (no decimals).";
            return false;
        }

        if (d > int.MaxValue)
        {
            errorMessage = $"{fieldLabel} is too large.";
            return false;
        }

        value = (int)d;
        return true;
    }

    public static string FormatOptionalMoney(double? value) =>
        value is { } v ? ((decimal)v).ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
}
