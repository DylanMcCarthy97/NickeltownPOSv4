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
}
