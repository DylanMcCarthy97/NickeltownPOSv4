using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopCatalogFilter
{
    public static bool MatchesSelectedChip(PitstopCatalogProductRow product, string chipKey) =>
        chipKey switch
        {
            PitstopCatalogChipKeys.All => true,
            PitstopCatalogChipKeys.Drinks => SubCategoryMatches(product, "drink"),
            PitstopCatalogChipKeys.Food => SubCategoryMatches(product, "food", "snack", "meal"),
            PitstopCatalogChipKeys.Merch => SubCategoryMatches(product, "merch", "merchandise"),
            _ => true,
        };

    public static IReadOnlyList<PitstopCatalogProductRow> FilterAndOrder(
        IEnumerable<PitstopCatalogProductRow> products,
        string chipKey)
    {
        var filtered = products.Where(r => MatchesSelectedChip(r, chipKey));
        if (string.Equals(chipKey, PitstopCatalogChipKeys.All, StringComparison.OrdinalIgnoreCase))
        {
            return filtered
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return filtered
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SubCategoryLabel)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static bool SubCategoryMatches(PitstopCatalogProductRow product, params string[] needles)
    {
        var sub = (product.SubCategoryLabel ?? string.Empty).Trim();
        if (sub.Length == 0)
        {
            sub = (product.CategoryName ?? string.Empty).Trim();
        }

        foreach (var needle in needles)
        {
            if (sub.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
