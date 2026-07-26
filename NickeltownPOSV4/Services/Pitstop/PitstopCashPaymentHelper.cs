using System;
using System.Collections.Generic;
using System.Globalization;

namespace NickeltownPOSV4.Services.Pitstop;

public static class PitstopCashPaymentHelper
{
    /// <summary>Common AUD note denominations for till quick-tender buttons.</summary>
    public static readonly decimal[] NoteDenominations = { 5m, 10m, 20m, 50m, 100m };

    public static decimal CalculateChange(decimal received, decimal saleTotal) =>
        decimal.Round(received - saleTotal, 2, MidpointRounding.AwayFromZero);

    public static bool IsConfirmEnabled(decimal received, decimal saleTotal, bool isCashSheetOpen) =>
        isCashSheetOpen && received >= saleTotal;

    public static bool IsShortWarning(decimal received, decimal saleTotal, bool isCashSheetOpen) =>
        isCashSheetOpen && received < saleTotal;

    /// <summary>
    /// Builds till-style quick tenders: Exact first, then next round-ups to common note sizes.
    /// </summary>
    public static IReadOnlyList<CashTenderOption> BuildQuickTenders(decimal saleTotal)
    {
        saleTotal = decimal.Round(Math.Max(0m, saleTotal), 2, MidpointRounding.AwayFromZero);
        var amounts = new List<decimal>(6);
        AddUnique(amounts, saleTotal);

        foreach (var denom in NoteDenominations)
        {
            var next = NextMultipleAtOrAbove(saleTotal, denom);
            if (next <= saleTotal)
            {
                next += denom;
            }

            AddUnique(amounts, next);
            if (amounts.Count >= 5)
            {
                break;
            }
        }

        var options = new List<CashTenderOption>(amounts.Count);
        for (var i = 0; i < amounts.Count; i++)
        {
            var amount = amounts[i];
            var isExact = i == 0;
            options.Add(new CashTenderOption(
                isExact ? "Exact" : FormatTenderLabel(amount),
                amount,
                isExact));
        }

        return options;
    }

    public static string FormatChangePreview(decimal received, decimal saleTotal)
    {
        if (received <= 0m)
        {
            return "—";
        }

        var change = CalculateChange(received, saleTotal);
        if (change < 0m)
        {
            return "Short " + FormatMoneyLabel(Math.Abs(change));
        }

        if (change == 0m)
        {
            return "No change";
        }

        return FormatMoneyLabel(change);
    }

    public static string FormatMoneyLabel(decimal amount) =>
        "$" + decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            .ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Compact till chip label ($5 / $20.50).</summary>
    public static string FormatTenderLabel(decimal amount)
    {
        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount == decimal.Truncate(amount))
        {
            return "$" + decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
        }

        return FormatMoneyLabel(amount);
    }

    private static decimal NextMultipleAtOrAbove(decimal value, decimal denom)
    {
        if (denom <= 0m)
        {
            return value;
        }

        if (value <= 0m)
        {
            return denom;
        }

        var multiples = Math.Ceiling((double)(value / denom));
        return decimal.Round((decimal)multiples * denom, 2, MidpointRounding.AwayFromZero);
    }

    private static void AddUnique(List<decimal> amounts, decimal value)
    {
        value = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (value < 0m)
        {
            return;
        }

        for (var i = 0; i < amounts.Count; i++)
        {
            if (amounts[i] == value)
            {
                return;
            }
        }

        amounts.Add(value);
    }
}

/// <summary>One quick-tender chip on the Pitstop cash till pad.</summary>
public readonly record struct CashTenderOption(string Label, decimal Amount, bool IsExact);
