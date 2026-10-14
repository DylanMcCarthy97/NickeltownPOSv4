using System;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>
/// Sale-time stock rules. <see cref="StockCatalogTaxonomy.StockMode"/> is the source of truth.
/// Food is never deducted (hot food has no counted amounts). Sell-until-gone auto-hides at 0.
/// </summary>
public static class StockSaleDeductionHelper
{
    /// <summary>
    /// When true, the sold line itself does not change on-hand qty
    /// (order-in, not tracked, food, or Shot+Mixer parent — mixer component is deducted separately).
    /// </summary>
    public static bool SkipsSoldItemStockDecrement(
        int trackStock,
        int orderInMerchandise,
        string? stockMode,
        string? itemType,
        string? itemName,
        string? catalogBucket = null,
        string? catalogSubCategory = null)
    {
        if (StockCatalogTaxonomy.IsFoodSubCategory(catalogBucket, catalogSubCategory))
        {
            return true;
        }

        var mode = ResolveEffectiveStockMode(trackStock, orderInMerchandise, stockMode);
        if (mode is StockCatalogTaxonomy.StockModeOrderIn or StockCatalogTaxonomy.StockModeNotTracked)
        {
            return true;
        }

        if (ShotMixerCatalog.IsShotMixerItem(itemName, itemType))
        {
            return true;
        }

        return false;
    }

    /// <summary>Legacy overload — category args ignored; kept so call sites compile during migration.</summary>
    public static bool SkipsSoldItemStockDecrement(
        int trackStock,
        int orderInMerchandise,
        string catalogBucket,
        string catalogSubCategory,
        string? itemType,
        string? itemName) =>
        SkipsSoldItemStockDecrement(
            trackStock,
            orderInMerchandise,
            stockMode: null,
            itemType,
            itemName,
            catalogBucket,
            catalogSubCategory);

    public static string ResolveEffectiveStockMode(int trackStock, int orderInMerchandise, string? stockMode)
    {
        if (!string.IsNullOrWhiteSpace(stockMode))
        {
            return StockCatalogTaxonomy.NormalizeStockMode(stockMode);
        }

        return StockCatalogTaxonomy.StockModeFromLegacyFlags(trackStock, orderInMerchandise, notGonnaOrderBack: 0);
    }

    /// <summary>
    /// Sell-until-gone items hide from the till when qty hits 0, and reappear when topped up.
    /// </summary>
    public static void SyncSellUntilGoneActiveFromQty(
        SqliteConnection conn,
        SqliteTransaction tx,
        long itemId)
    {
        if (itemId <= 0)
        {
            return;
        }

        conn.Execute(
            """
            UPDATE Items
            SET IsActive = CASE WHEN COALESCE(StockQty, 0) > 0 THEN 1 ELSE 0 END,
                UpdatedAt = datetime('now')
            WHERE Id = @id
              AND (
                lower(trim(COALESCE(NULLIF(TRIM(StockMode), ''), ''))) = 'selluntilgone'
                OR COALESCE(NotGonnaOrderBack, 0) != 0
              )
            """,
            new { id = itemId },
            tx);
    }

    public static void DeductMixerComponentStock(
        SqliteConnection conn,
        SqliteTransaction tx,
        string? itemDescription,
        int saleQuantity,
        string movementReference)
    {
        var meta = StockItemMetadataSerializer.Parse(itemDescription, isShotMixer: true);
        if (meta.MixerItemId is not long mixerId || mixerId <= 0)
            return;

        var delta = -(Math.Max(1, meta.MixerQty) * Math.Max(1, saleQuantity));
        // Deduct when StockMode maintains on-hand qty (Tracked / SellUntilGone), not only legacy flags.
        conn.Execute(
            """
            UPDATE Items
            SET StockQty = MAX(0, StockQty + @delta), UpdatedAt = datetime('now')
            WHERE Id = @id
              AND lower(trim(COALESCE(NULLIF(TRIM(StockMode), ''), 'Tracked'))) IN ('tracked', 'selluntilgone')
              AND COALESCE(OrderInMerchandise, 0) = 0
            """,
            new { delta, id = mixerId },
            tx);

        conn.Execute(
            """
            INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
            VALUES (@ItemId, @Delta, @Reason, @Ref, datetime('now'))
            """,
            new { ItemId = mixerId, Delta = delta, Reason = "ShotMixerSale", Ref = movementReference },
            tx);

        SyncSellUntilGoneActiveFromQty(conn, tx, mixerId);
    }
}
