using System;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.Services.Stock;

internal static class StockSaleDeductionHelper
{
    public static bool SkipsSoldItemStockDecrement(
        int trackStock,
        int orderInMerchandise,
        string catalogBucket,
        string catalogSubCategory,
        string? itemType,
        string? itemName)
    {
        if (orderInMerchandise != 0 || trackStock == 0)
            return true;
        if (ShotMixerCatalog.IsShotMixerItem(itemName, itemType))
            return true;
        return StockCatalogTaxonomy.SkipsBarStockDecrement(catalogBucket, catalogSubCategory);
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
        conn.Execute(
            """
            UPDATE Items
            SET StockQty = MAX(0, StockQty + @delta), UpdatedAt = datetime('now')
            WHERE Id = @id AND COALESCE(TrackStock, 1) != 0 AND COALESCE(OrderInMerchandise, 0) = 0
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
    }
}