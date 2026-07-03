using System;
using System.Globalization;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Data.Sqlite;

public static class CatalogSpecialValueResolver
{
    public const string FixedPrice = "FixedPrice";
    public const string PercentOff = "PercentOff";

    public static string NormalizeType(string? specialType)
    {
        var t = (specialType ?? string.Empty).Trim();
        if (string.Equals(t, PercentOff, StringComparison.OrdinalIgnoreCase))
        {
            return PercentOff;
        }

        return FixedPrice;
    }

    public static bool TryParsePercent(string? text, out decimal percent)
    {
        percent = 0m;
        var s = (text ?? string.Empty).Trim();
        if (s.EndsWith('%'))
        {
            s = s[..^1].Trim();
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var p)
            && !decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out p))
        {
            return false;
        }

        if (p <= 0m || p >= 100m)
        {
            return false;
        }

        percent = decimal.Round(p, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    public static decimal RoundSalePrice(decimal price) =>
        decimal.Round(price, 2, MidpointRounding.AwayFromZero);

    public static bool TryResolveSaleUnitPrice(
        string? specialType,
        string? specialValueText,
        decimal regularUnitPrice,
        out decimal saleUnitPrice,
        out string errorMessage)
    {
        saleUnitPrice = 0m;
        errorMessage = string.Empty;

        if (regularUnitPrice <= 0m)
        {
            errorMessage = "Set a regular shelf price before enabling a special.";
            return false;
        }

        var type = NormalizeType(specialType);
        var value = (specialValueText ?? string.Empty).Trim();

        if (type == PercentOff)
        {
            if (!TryParsePercent(value, out var pct))
            {
                errorMessage = "Enter a percentage off between 1 and 99 (e.g. 10 for 10% off).";
                return false;
            }

            saleUnitPrice = RoundSalePrice(regularUnitPrice * (100m - pct) / 100m);
        }
        else
        {
            if (!StockMoneyInputParser.TryParseMoney(value, out saleUnitPrice) || saleUnitPrice <= 0m)
            {
                errorMessage = "Enter a special price greater than zero, or turn special off.";
                return false;
            }

            saleUnitPrice = RoundSalePrice(saleUnitPrice);
        }

        if (saleUnitPrice <= 0m)
        {
            errorMessage = "Special price must be greater than zero.";
            return false;
        }

        if (saleUnitPrice >= regularUnitPrice)
        {
            errorMessage = "Special price must be lower than the regular shelf price.";
            return false;
        }

        return true;
    }

    public static bool TryFormatStoredValueForUi(string? specialType, string? storedValue, out string displayValue)
    {
        displayValue = (storedValue ?? string.Empty).Trim();
        if (displayValue.Length == 0)
        {
            return false;
        }

        if (NormalizeType(specialType) != PercentOff)
        {
            return true;
        }

        if (TryParsePercent(displayValue, out var pct))
        {
            displayValue = pct.ToString("0.##", CultureInfo.InvariantCulture);
            return true;
        }

        return true;
    }
}