using System;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Shared rules for when catalog tiles should show promotional pricing.</summary>
public static class CatalogSpecialPricing
{
    public static decimal GetBarRegularUnitPrice(decimal barPrice, decimal guestPrice, bool isGuestTab) =>
        isGuestTab && guestPrice > 0m ? guestPrice : barPrice;

    public static bool ShouldShowBarSpecialPrice(
        bool usesOpenPrice,
        int isOnSpecial,
        decimal barPrice,
        decimal guestPrice,
        decimal barSpecialPrice,
        decimal guestSpecialPrice,
        bool isGuestTab,
        out decimal regularUnitPrice,
        out decimal saleUnitPrice)
    {
        regularUnitPrice = GetBarRegularUnitPrice(barPrice, guestPrice, isGuestTab);
        saleUnitPrice = BarCatalogPriceResolver.GetUnitPriceForBarAdd(
            barPrice,
            guestPrice,
            barSpecialPrice,
            guestSpecialPrice,
            isOnSpecial,
            isGuestTab);

        if (usesOpenPrice || isOnSpecial == 0)
        {
            return false;
        }

        if (saleUnitPrice <= 0m || regularUnitPrice <= 0m)
        {
            return false;
        }

        return saleUnitPrice < regularUnitPrice;
    }

    public static bool ShouldShowPitstopSpecialPrice(
        int isOnSpecial,
        double pitstopPrice,
        double pitstopSpecialPrice,
        out decimal regularUnitPrice,
        out decimal saleUnitPrice)
    {
        regularUnitPrice = decimal.Round((decimal)pitstopPrice, 2, MidpointRounding.AwayFromZero);
        saleUnitPrice = decimal.Round(
            (decimal)BarCatalogPriceResolver.GetPitstopUnitPrice(
                (decimal)pitstopPrice,
                (decimal)pitstopSpecialPrice,
                isOnSpecial),
            2,
            MidpointRounding.AwayFromZero);

        if (isOnSpecial == 0 || saleUnitPrice <= 0m || regularUnitPrice <= 0m)
        {
            return false;
        }

        return saleUnitPrice < regularUnitPrice;
    }
}