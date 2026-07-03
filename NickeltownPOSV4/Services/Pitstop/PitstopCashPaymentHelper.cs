using System;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopCashPaymentHelper
{
    public static decimal CalculateChange(decimal received, decimal saleTotal) =>
        decimal.Round(received - saleTotal, 2, MidpointRounding.AwayFromZero);

    public static bool IsConfirmEnabled(decimal received, decimal saleTotal, bool isCashSheetOpen) =>
        isCashSheetOpen && received >= saleTotal;

    public static bool IsShortWarning(decimal received, decimal saleTotal, bool isCashSheetOpen) =>
        isCashSheetOpen && received < saleTotal;
}
