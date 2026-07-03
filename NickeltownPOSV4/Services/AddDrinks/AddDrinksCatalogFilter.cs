using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksCatalogFilter
{
    private const string CategorySeparator = " / ";

    /// <summary>
    /// Canonical known chip labels. Used by <see cref="IsKnownChipCategory"/>
    /// to short-circuit resolution when a product's normalized sub-category
    /// already matches a chip. The dynamic chip row is built by
    /// <see cref="BuildCategoryChipLabels"/> from the visible products.
    /// </summary>
    public static readonly string[] FixedCategoryLabels =
    {
        AddDrinksPanelViewModel.AllCategoriesToken,
        AddDrinksPanelViewModel.FavoritesCategoryLabel,
        "Beer",
        "Drinks",
        "Food",
        "Merch",
        "Spirits",
        "Wine",
    };

    /// <summary>
    /// Preferred display order for the product-category chips. Categories not in
    /// this list (e.g. a new sub-category added later) are appended in
    /// alphabetical order, but always before Favourites and All.
    /// </summary>
    private static readonly string[] PreferredProductCategoryOrder =
    {
        "Beer", "Drinks", "Merch", "Food", "Spirits", "Wine",
    };

    /// <summary>
    /// Builds the category chip row dynamically:
    /// <list type="bullet">
    ///   <item><description>Product categories first in a fixed visual order
    ///   (Beer, Drinks, Merch, Food, Spirits, Wine), restricted to the ones
    ///   that actually exist in <paramref name="products"/>.</description></item>
    ///   <item><description>Any unexpected categories are appended in alphabetical order.</description></item>
    ///   <item><description>"Favourites" is always last. "All" is the implicit default
    ///   filter state and is not rendered as a chip; the panel header exposes a
    ///   small "Clear filter" button instead.</description></item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> BuildCategoryChipLabels(IEnumerable<DrinkCardItem>? products)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (products is not null)
        {
            foreach (var p in products)
            {
                if (p is null)
                {
                    continue;
                }

                var label = (ResolveProductChipCategory(p) ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                if (CategoryEquals(label, AddDrinksPanelViewModel.AllCategoriesToken)
                    || CategoryEquals(label, AddDrinksPanelViewModel.FavoritesCategoryLabel))
                {
                    continue;
                }

                seen.Add(label);
            }
        }

        var list = new List<string>(seen.Count + 2);
        foreach (var preferred in PreferredProductCategoryOrder)
        {
            if (seen.Any(s => CategoryEquals(s, preferred)))
            {
                list.Add(preferred);
            }
        }

        foreach (var extra in seen.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (PreferredProductCategoryOrder.Any(p => CategoryEquals(p, extra)))
            {
                continue;
            }

            list.Add(extra);
        }

        list.Add(AddDrinksPanelViewModel.FavoritesCategoryLabel);
        return list;
    }

    public readonly record struct FilterResult(IReadOnlyList<DrinkCardItem> Items, string? FavoritesStatusHint);

    public static FilterResult Filter(
        IReadOnlyList<DrinkCardItem> allProducts,
        string selectedCategory,
        IReadOnlyList<long> favoriteOrderedIds,
        string favoritesCategoryLabel,
        string allCategoriesToken)
    {
        var selected = (selectedCategory ?? string.Empty).Trim();
        if (string.Equals(selected, favoritesCategoryLabel, StringComparison.OrdinalIgnoreCase))
        {
            string? hint = null;
            if (favoriteOrderedIds.Count == 0)
            {
                hint = "No favourites yet — tap ♥ on a drink card to pin it here.";
            }

            var favItems = new List<DrinkCardItem>();
            foreach (var id in favoriteOrderedIds)
            {
                var d = allProducts.FirstOrDefault(x => x.ItemId == id);
                if (d is not null)
                {
                    favItems.Add(d);
                }
            }

            return new FilterResult(favItems, hint);
        }

        IEnumerable<DrinkCardItem> q = allProducts;
        if (!string.Equals(selected, allCategoriesToken, StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(d => ProductMatchesChip(d, selected));
        }

        return new FilterResult(
            q.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            null);
    }

    /// <summary>
    /// Whether the "Shot + Mixer" tile appears in the catalog grid for the given
    /// category filter (sorted alphabetically with other products). Shot + Mixer is a
    /// composite workflow rather than a real product, so it is never a saved
    /// favourite and is intentionally hidden when the user filters by Favourites
    /// (the favourites view should only show items the user has actually
    /// hearted). It surfaces on the default "All" view and the Spirits chip.
    /// </summary>
    public static bool ShowShotMixerQuickAction(string? selectedCategory) =>
        CategoryEquals(selectedCategory, AddDrinksPanelViewModel.AllCategoriesToken)
        || CategoryEquals(selectedCategory, "Spirits");

    public static string ResolveProductChipCategory(DrinkCardItem product)
    {
        var fromCatalog = SubCategoryFromKey(NormalizeCategoryKey(product.CategoryName));
        if (IsKnownChipCategory(fromCatalog))
        {
            return fromCatalog;
        }

        var haystack = $"{product.CategoryName} {product.Name} {product.ItemType}";
        if (ContainsAny(haystack, "soft drink", "softdrink", "mixer", "cordial", "soda", "pop top", "poptop"))
        {
            return "Drinks";
        }

        if (ContainsAny(haystack, "beer", "lager", "ale", "cider"))
        {
            return "Beer";
        }

        if (ContainsAny(haystack, "wine", "champagne", "prosecco"))
        {
            return "Wine";
        }

        if (ContainsAny(haystack, "spirit", "vodka", "whisky", "whiskey", "rum", "gin", "bourbon"))
        {
            return "Spirits";
        }

        if (ContainsAny(haystack, "food", "snack", "meal", "appetizer"))
        {
            return "Food";
        }

        if (ContainsAny(haystack, "merch", "merchandise", "apparel", "shirt", "hat", "cap", "hoodie"))
        {
            return "Merch";
        }

        return fromCatalog;
    }

    public static bool ProductMatchesChip(DrinkCardItem product, string chipCategory) =>
        CategoryEquals(ResolveProductChipCategory(product), chipCategory);

    private static bool IsKnownChipCategory(string sub) =>
        FixedCategoryLabels.Any(c =>
            !CategoryEquals(c, AddDrinksPanelViewModel.AllCategoriesToken)
            && !CategoryEquals(c, AddDrinksPanelViewModel.FavoritesCategoryLabel)
            && CategoryEquals(c, sub));

    private static bool CategoryEquals(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeCategoryKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return StockCatalogTaxonomy.CatalogDisplayName(
                StockCatalogTaxonomy.BucketBar,
                StockCatalogTaxonomy.DefaultSubCategory(StockCatalogTaxonomy.BucketBar));
        }

        var t = raw.Trim();
        var sep = t.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        if (sep >= 0)
        {
            var bucket = StockCatalogTaxonomy.NormalizeBucket(t[..sep].Trim());
            var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, t[(sep + CategorySeparator.Length)..].Trim());
            return StockCatalogTaxonomy.CatalogDisplayName(bucket, sub);
        }

        var barBucket = StockCatalogTaxonomy.BucketBar;
        var barSub = StockCatalogTaxonomy.NormalizeSubCategory(barBucket, t);
        return StockCatalogTaxonomy.CatalogDisplayName(barBucket, barSub);
    }

    public static string SubCategoryFromKey(string categoryKey)
    {
        var normalized = NormalizeCategoryKey(categoryKey);
        var sep = normalized.LastIndexOf(CategorySeparator, StringComparison.Ordinal);
        return sep >= 0 ? normalized[(sep + CategorySeparator.Length)..].Trim() : normalized;
    }
}
