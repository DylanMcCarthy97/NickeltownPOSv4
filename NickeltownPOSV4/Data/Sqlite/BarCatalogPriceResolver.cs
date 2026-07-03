using System;

namespace NickeltownPOSV4.Data.Sqlite;

public static class BarCatalogPriceResolver
{
    public static decimal GetUnitPriceForBarAdd(
        decimal barPrice,
        decimal guestPrice,
        decimal barSpecialPrice,
        decimal guestSpecialPrice,
        int isOnSpecial,
        bool isGuestTab)
    {
        if (isOnSpecial != 0 && barSpecialPrice > 0m)
        {
            if (isGuestTab && guestSpecialPrice > 0m) return guestSpecialPrice;
            return barSpecialPrice;
        }

        if (isGuestTab) return guestPrice > 0m ? guestPrice : barPrice;
        return barPrice;
    }

    public static decimal GetPitstopUnitPrice(decimal pitstopPrice, decimal pitstopSpecialPrice, int isOnSpecial) =>
        isOnSpecial != 0 && pitstopSpecialPrice > 0.00001m ? pitstopSpecialPrice : pitstopPrice;

    /// <summary>
    /// Pitstop retail price: explicit Pitstop price when set; otherwise bar price for Shared-bucket items.
    /// </summary>
    public static double ResolveEffectivePitstopPrice(double pitstopPrice, double barPrice, string? catalogBucket)
    {
        if (pitstopPrice > 0.00001d)
        {
            return pitstopPrice;
        }

        if (barPrice > 0.00001d
            && string.Equals(
                StockCatalogTaxonomy.NormalizeBucket(catalogBucket),
                StockCatalogTaxonomy.BucketShared,
                StringComparison.Ordinal))
        {
            return barPrice;
        }

        return pitstopPrice;
    }

    public static decimal ResolveEffectivePitstopPrice(decimal pitstopPrice, decimal barPrice, string? catalogBucket) =>
        (decimal)ResolveEffectivePitstopPrice((double)pitstopPrice, (double)barPrice, catalogBucket);
}
