using System;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>
/// Category-based membership for shopping lists.
/// Uses <see cref="StockEditorRow.CatalogSubCategory"/> (not product names).
/// </summary>
public static class ShoppingListCatalog
{
    /// <summary>
    /// Bar list: Beer, Wine, Spirits, Drinks (soft drinks / water / premix misc).
    /// Explicitly excludes Merch and Food (hot food is not counted stock).
    /// </summary>
    public static bool IsBarShoppingCategory(StockEditorRow row)
    {
        if (StockInventoryLevelHelper.IsMerchItem(row))
        {
            return false;
        }

        if (StockCatalogTaxonomy.IsFoodSubCategory(row.CatalogBucket, row.CatalogSubCategory))
        {
            return false;
        }

        var sub = StockCatalogTaxonomy.NormalizeSubCategory(
            StockCatalogTaxonomy.NormalizeBucket(row.CatalogBucket),
            row.CatalogSubCategory);

        return sub.Equals("Beer", StringComparison.OrdinalIgnoreCase)
            || sub.Equals("Wine", StringComparison.OrdinalIgnoreCase)
            || sub.Equals("Spirits", StringComparison.OrdinalIgnoreCase)
            || sub.Equals("Drinks", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMerchandiseShoppingCategory(StockEditorRow row) =>
        StockInventoryLevelHelper.IsMerchItem(row);

    public static bool BelongsTo(ShoppingListKind kind, StockEditorRow row) =>
        kind == ShoppingListKind.Merchandise
            ? IsMerchandiseShoppingCategory(row)
            : IsBarShoppingCategory(row);

    public static string DisplayTitle(ShoppingListKind kind) =>
        kind == ShoppingListKind.Merchandise
            ? "Merchandise Shopping List"
            : "Weekly Bar Shopping List";

    public static string ShortLabel(ShoppingListKind kind) =>
        kind == ShoppingListKind.Merchandise ? "Merchandise" : "Bar";

    public static string FileStem(ShoppingListKind kind) =>
        kind == ShoppingListKind.Merchandise ? "Merch_Shopping_List" : "Bar_Shopping_List";
}
