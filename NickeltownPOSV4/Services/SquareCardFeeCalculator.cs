using System;

namespace NickeltownPOSV4.Services;

public static class SquareCardFeeCalculator
{
    public static decimal RoundToNearestFiveCents(decimal amount) =>
        Math.Ceiling(amount * 20m) / 20m;

    public static (decimal unroundedCardTotal, decimal roundedCardTotal, decimal surcharge) CalculateCardTotal(
        decimal baseTotal,
        decimal feePercent)
    {
        if (baseTotal <= 0m)
        {
            return (0m, 0m, 0m);
        }

        var rate = feePercent / 100m;
        var unroundedCardTotal = baseTotal * (1m + rate);
        var roundedCardTotal = RoundToNearestFiveCents(unroundedCardTotal);
        var surcharge = roundedCardTotal - baseTotal;
        if (surcharge < 0m)
        {
            surcharge = 0m;
        }

        return (unroundedCardTotal, roundedCardTotal, surcharge);
    }

    /// <summary>
    /// Splits a Square gross amount that may include a customer card surcharge.
    /// When <paramref name="surchargePercent"/> is 0 (feature disabled), the whole gross is product sales.
    /// </summary>
    public static (decimal productSales, decimal surcharge) SplitGrossExcludingSurcharge(
        decimal grossInclusive,
        decimal surchargePercent)
    {
        var gross = decimal.Round(grossInclusive, 2, MidpointRounding.AwayFromZero);
        if (gross <= 0m)
        {
            return (0m, 0m);
        }

        if (surchargePercent <= 0m)
        {
            return (gross, 0m);
        }

        var rate = surchargePercent / 100m;
        var productSales = decimal.Round(gross / (1m + rate), 2, MidpointRounding.AwayFromZero);
        var surcharge = decimal.Round(gross - productSales, 2, MidpointRounding.AwayFromZero);
        if (surcharge < 0m)
        {
            surcharge = 0m;
            productSales = gross;
        }

        return (productSales, surcharge);
    }
}
