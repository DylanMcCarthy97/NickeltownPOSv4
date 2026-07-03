using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksMixerHelper
{
    public static bool IsMixerCategory(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return categoryName.Contains("mixer", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeShot(DrinkCardItem item)
    {
        if (IsMixerCategory(item.CategoryName))
        {
            return false;
        }

        var it = item.ItemType ?? string.Empty;
        if (it.Contains("shot", StringComparison.OrdinalIgnoreCase)
            && !it.Contains("shotgun", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var cat = item.CategoryName ?? string.Empty;
        return cat.Contains("shot", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<MixerPickerChoice> GetMixerChoices(
        IEnumerable<DrinkCardItem> allProducts,
        int maxChoices = 12) =>
        GetShotMixerChoices(allProducts)
            .Take(maxChoices)
            .ToList();

    /// <summary>Bar soft-drink / mixer SKUs for Shot + Mixer (shows stock; OOS rows are disabled).</summary>
    public static IReadOnlyList<MixerPickerChoice> GetShotMixerChoices(IEnumerable<DrinkCardItem> allProducts) =>
        allProducts
            .Where(IsShotMixerCandidate)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToMixerChoice)
            .ToList();

    public static bool IsShotMixerCandidate(DrinkCardItem item)
    {
        if (IsMixerCategory(item.CategoryName))
        {
            return true;
        }

        var cat = item.CategoryName ?? string.Empty;
        if (!cat.Contains("Drinks", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (cat.Contains("Spirit", StringComparison.OrdinalIgnoreCase)
            || cat.Contains("Beer", StringComparison.OrdinalIgnoreCase)
            || cat.Contains("Wine", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static MixerPickerChoice ToMixerChoice(DrinkCardItem p) =>
        new(
            p.ItemId,
            p.Name,
            p.UsesOpenPrice ? string.Empty : p.PriceText,
            p.StockLabel,
            p.CanAddFromCatalog);
}
