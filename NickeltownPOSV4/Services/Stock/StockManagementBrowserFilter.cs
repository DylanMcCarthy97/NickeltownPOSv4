using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Volunteer filter chips for the stock home screen.</summary>
internal static class StockManagementBrowserFilter
{
    public const int ChipAll = 0;

    public const int ChipNeedBuying = 1;

    public const int ChipOutOfStock = 2;

    public const int ChipDrinks = 3;

    public const int ChipMerch = 4;

    public const int ChipInactive = 5;

    public static IReadOnlyList<StockEditorRow> Apply(
        IReadOnlyList<StockEditorRow> all,
        string searchText,
        int browserFilterIndex)
    {
        IEnumerable<StockEditorRow> q = all;

        if (browserFilterIndex != ChipInactive)
        {
            q = q.Where(r => r.IsActive != 0);
        }

        var needle = (searchText ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            q = q.Where(r =>
                (r.Name ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (r.Sku ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || StockAlternateSkuHelper.AlternateSkuListContains(r.AlternateSkusJson, needle)
                || (r.CategoryName ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (r.CatalogSubCategory ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        switch (browserFilterIndex)
        {
            case ChipAll:
                break;
            case ChipNeedBuying:
                q = q.Where(StockInventoryLevelHelper.NeedsBuying);
                break;
            case ChipOutOfStock:
                q = q.Where(r =>
                    StockInventoryLevelHelper.ResolveStatus(r) == StockVolunteerStatus.OutOfStock);
                break;
            case ChipDrinks:
                q = q.Where(r => SubCategoryEquals(r, "Drinks"));
                break;
            case ChipMerch:
                q = q.Where(r => SubCategoryEquals(r, "Merch"));
                break;
            case ChipInactive:
                q = q.Where(r => r.IsActive == 0);
                break;
        }

        return q.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static int CountForFilter(IReadOnlyList<StockEditorRow> all, int filterIndex) =>
        Apply(all, string.Empty, filterIndex).Count;

    private static bool SubCategoryEquals(StockEditorRow r, string sub) =>
        (r.CatalogSubCategory ?? string.Empty).Trim().Equals(sub, StringComparison.OrdinalIgnoreCase);
}
