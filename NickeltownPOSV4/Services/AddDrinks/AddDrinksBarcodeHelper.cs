using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

/// <summary>SKU / alternate barcode lookup for wedge scanners (delegates to shared stock helper).</summary>
internal static class AddDrinksBarcodeHelper
{
    public static DrinkCardItem? FindProduct(IReadOnlyList<DrinkCardItem> products, string code)
    {
        foreach (var candidate in StockAlternateSkuHelper.ExpandScanCandidates(code))
        {
            var hit = products.FirstOrDefault(p =>
                (!string.IsNullOrWhiteSpace(p.Sku)
                    && string.Equals(p.Sku.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
                || p.AlternateSkus.Any(a =>
                    string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase)));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    public static IEnumerable<string> ExpandScanCandidates(string code) =>
        StockAlternateSkuHelper.ExpandScanCandidates(code);

    public static IReadOnlyList<string> ParseAlternateSkusJson(string? json) =>
        StockAlternateSkuHelper.ParseAlternateSkuList(json).ToList();
}
