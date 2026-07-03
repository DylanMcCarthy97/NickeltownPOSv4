using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class AddDrinksCatalogPager
{
    public static int ProductsOnFirstPage(int pageSize, bool shotMixerOnFirstPage) =>
        shotMixerOnFirstPage ? Math.Max(1, pageSize - 1) : pageSize;

    public static int TotalPages(int productCount, int pageSize, bool shotMixerOnFirstPage)
    {
        if (productCount <= 0)
        {
            return 1;
        }

        var first = ProductsOnFirstPage(pageSize, shotMixerOnFirstPage);
        if (productCount <= first)
        {
            return 1;
        }

        var remaining = productCount - first;
        return 1 + (int)Math.Ceiling(remaining / (double)pageSize);
    }

    public static int ClampPage(int page, int productCount, int pageSize, bool shotMixerOnFirstPage)
    {
        var total = TotalPages(productCount, pageSize, shotMixerOnFirstPage);
        if (page > total)
        {
            return total;
        }

        return page < 1 ? 1 : page;
    }

    public static int ProductOffset(int page, int pageSize, bool shotMixerOnFirstPage)
    {
        if (page <= 1)
        {
            return 0;
        }

        var first = ProductsOnFirstPage(pageSize, shotMixerOnFirstPage);
        return first + (page - 2) * pageSize;
    }

    public static int ProductTake(int page, int pageSize, bool shotMixerOnFirstPage) =>
        page == 1 ? ProductsOnFirstPage(pageSize, shotMixerOnFirstPage) : pageSize;

    public static IReadOnlyList<DrinkCardItem> GetProductsPage(
        IReadOnlyList<DrinkCardItem> filtered,
        int page,
        int pageSize,
        bool shotMixerOnFirstPage) =>
        filtered.Skip(ProductOffset(page, pageSize, shotMixerOnFirstPage))
            .Take(ProductTake(page, pageSize, shotMixerOnFirstPage))
            .ToList();

    public static int TotalPages(int itemCount, int pageSize) =>
        TotalPages(itemCount, pageSize, shotMixerOnFirstPage: false);

    /// <summary>Page count for <see cref="GetObjectPage"/> (0 when the catalog is empty).</summary>
    public static int TotalObjectPages(int entryCount, int pageSize)
    {
        if (entryCount <= 0 || pageSize <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(entryCount / (double)pageSize);
    }

    public static int ClampObjectPage(int page, int entryCount, int pageSize)
    {
        var total = TotalObjectPages(entryCount, pageSize);
        if (total <= 0)
        {
            return 1;
        }

        if (page > total)
        {
            return total;
        }

        return page < 1 ? 1 : page;
    }

    public static int ClampPage(int page, int itemCount, int pageSize) =>
        ClampPage(page, itemCount, pageSize, shotMixerOnFirstPage: false);

    public static IReadOnlyList<DrinkCardItem> GetPage(
        IReadOnlyList<DrinkCardItem> filtered,
        int page,
        int pageSize) =>
        GetProductsPage(filtered, page, pageSize, shotMixerOnFirstPage: false);

    /// <summary>Merges bar products with the Shot + Mixer tile and sorts A–Z by display name.</summary>
    public static List<object> BuildSortedCatalogEntries(
        IReadOnlyList<DrinkCardItem> products,
        ShotMixerCatalogTile? shotMixerTile)
    {
        var list = new List<object>(products.Count + (shotMixerTile is not null ? 1 : 0));
        foreach (var p in products)
        {
            list.Add(p);
        }

        if (shotMixerTile is not null)
        {
            list.Add(shotMixerTile);
        }

        list.Sort(CompareCatalogEntries);
        return list;
    }

    public static IReadOnlyList<object> GetObjectPage(
        IReadOnlyList<object> entries,
        int page,
        int pageSize) =>
        entries.Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(pageSize)
            .ToList();

    private static int CompareCatalogEntries(object a, object b) =>
        string.Compare(GetCatalogSortName(a), GetCatalogSortName(b), StringComparison.OrdinalIgnoreCase);

    private static string GetCatalogSortName(object item) =>
        item switch
        {
            DrinkCardItem d => d.Name ?? string.Empty,
            ShotMixerCatalogTile => ShotMixerCatalog.ItemName,
            _ => string.Empty,
        };
}
