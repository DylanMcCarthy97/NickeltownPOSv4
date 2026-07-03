using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopCartHelper
{
    public static decimal GetCartTotal(IEnumerable<PitstopCartLineViewModel> lines) =>
        decimal.Round(lines.Sum(l => l.UnitPrice * l.Quantity), 2, MidpointRounding.AwayFromZero);

    public static int QuantityInCart(IEnumerable<PitstopCartLineViewModel> cart, long itemId) =>
        cart.Where(l => l.ItemId == itemId).Sum(l => l.Quantity);

    public static int MaxAllowedQuantity(PitstopCatalogProductRow row) =>
        row.OrderInMerchandise != 0 || row.TrackStock == 0 ? int.MaxValue : row.StockQty;

    public static bool TryValidateAddQuantity(
        PitstopCatalogProductRow row,
        int existingLineQty,
        int otherLinesQty,
        int qtyToAdd,
        out string? errorMessage)
    {
        errorMessage = null;
        if (row.TrackStock == 0 || row.OrderInMerchandise != 0)
        {
            return true;
        }

        var max = row.StockQty;
        if (existingLineQty + otherLinesQty + qtyToAdd > max)
        {
            errorMessage = $"Not enough stock for \"{row.Name}\" (have {row.StockQty}).";
            return false;
        }

        return true;
    }

    public static bool TryValidateDeltaQuantity(
        PitstopCatalogProductRow? row,
        int lineQty,
        int otherLinesQty,
        int nextQty,
        out string? errorMessage)
    {
        errorMessage = null;
        if (row is null)
        {
            return true;
        }

        var track = row.TrackStock;
        var orderIn = row.OrderInMerchandise != 0;
        if (orderIn || track == 0)
        {
            return true;
        }

        if (nextQty + otherLinesQty > row.StockQty)
        {
            errorMessage = $"Not enough stock (have {row.StockQty}).";
            return false;
        }

        return true;
    }
}
